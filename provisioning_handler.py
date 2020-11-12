# ------------------------------------------------------------------------------
# Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
# SPDX-License-Identifier: MIT-0 
# ------------------------------------------------------------------------------
# Demonstrates how to call/orchestrate AWS fleet provisioning services
#  with a provided bootstrap certificate (aka - provisioning claim cert).
#   
# Initial version - Raleigh Murch, AWS
# email: murchral@amazon.com
# ------------------------------------------------------------------------------

from awscrt import io, mqtt, auth, http
from awsiot import mqtt_connection_builder
from utils.config_loader import Config
import time
import logging
import json 
import os
import asyncio
import glob


class ProvisioningHandler:

    def __init__(self, file_path):
        """Initializes the provisioning handler
        
        Arguments:
            file_path {string} -- path to your configuration file
        """
        #Logging
        logging.basicConfig(level=logging.ERROR)
        self.logger = logging.getLogger(__name__)
        
        #Load configuration settings from config.ini
        config = Config(file_path)
        self.config_parameters = config.get_section('SETTINGS')
        self.secure_cert_path = self.config_parameters['SECURE_CERT_PATH']
        self.iot_endpoint = self.config_parameters['IOT_ENDPOINT']	
        self.template_name = self.config_parameters['PRODUCTION_TEMPLATE']
        self.rotation_template = self.config_parameters['CERT_ROTATION_TEMPLATE']
        self.claim_cert = self.config_parameters['CLAIM_CERT']
        self.secure_key = self.config_parameters['SECURE_KEY']
        self.root_cert = self.config_parameters['ROOT_CERT']
    
        # Sample Provisioning Template requests a serial number as a 
        # seed to generate Thing names in IoTCore. Simulating here.
        #self.unique_id = str(int(round(time.time() * 1000)))
        self.unique_id = "1234567-abcde-fghij-klmno-1234567abc-TLS350" 

        # ------------------------------------------------------------------------------
        #  -- PROVISIONING HOOKS EXAMPLE --
        # Provisioning Hooks are a powerful feature for fleet provisioning. Most of the
        # heavy lifting is performed within the cloud lambda. However, you can send
        # device attributes to be validated by the lambda. An example is shown in the line
        # below (.hasValidAccount could be checked in the cloud against a database). 
        # Alternatively, a serial number, geo-location, or any attribute could be sent.
        # 
        # -- Note: This attribute is passed up as part of the register_thing method and
        # will be validated in your lambda's event data.
        # ------------------------------------------------------------------------------

        self.primary_MQTTClient = None
        self.test_MQTTClient = None
        self.callback_returned = False
        self.message_payload = {}
        self.isRotation = False


    def core_connect(self):
        """ Method used to connect to AWS IoTCore Service. Endpoint collected from config.
        
        """
        if self.isRotation:
            self.logger.info('##### CONNECTING WITH EXISTING CERT #####')
            print('##### CONNECTING WITH EXISTING CERT #####')
            self.get_current_certs()
        else:
            self.logger.info('##### CONNECTING WITH PROVISIONING CLAIM CERT #####')
            print('##### CONNECTING WITH PROVISIONING CLAIM CERT #####')

        event_loop_group = io.EventLoopGroup(1)
        host_resolver = io.DefaultHostResolver(event_loop_group)
        client_bootstrap = io.ClientBootstrap(event_loop_group, host_resolver)

        self.primary_MQTTClient = mqtt_connection_builder.mtls_from_path(
            endpoint=self.iot_endpoint,
            cert_filepath="{}/{}".format(self.secure_cert_path, self.claim_cert),
            pri_key_filepath="{}/{}".format(self.secure_cert_path, self.secure_key),
            client_bootstrap=client_bootstrap,
            ca_filepath="{}/{}".format(self.secure_cert_path, self.root_cert),
            on_connection_interrupted=self.on_connection_interrupted,
            on_connection_resumed=self.on_connection_resumed,
            client_id=self.unique_id,
            clean_session=False,
            keep_alive_secs=6)
        
        print("Connecting to {} with client ID '{}'...".format(self.iot_endpoint, self.unique_id))
        connect_future = self.primary_MQTTClient.connect()
        # Future.result() waits until a result is available
        connect_future.result()
        print("Connected!")


    def on_connection_interrupted(self, connection, error, **kwargs):
        print('connection interrupted with error {}'.format(error))


    def on_connection_resumed(self, connection, return_code, session_present, **kwargs):
        print('connection resumed with return code {}, session present {}'.format(return_code, session_present))


    def get_current_certs(self):
        non_bootstrap_certs = glob.glob('{}/[!boot]*.crt'.format(self.secure_cert_path))
        non_bootstrap_key = glob.glob('{}/[!boot]*.key'.format(self.secure_cert_path))

        #Get the current cert
        if len(non_bootstrap_certs) > 0:
            self.claim_cert = os.path.basename(non_bootstrap_certs[0])

        #Get the current key
        if len(non_bootstrap_key) > 0:
            self.secure_key = os.path.basename(non_bootstrap_key[0])
        

    def enable_error_monitor(self):
        """ Subscribe to pertinent IoTCore topics that would emit errors
        """

        template_reject_topic = "$aws/provisioning-templates/{}/provision/json/rejected".format(self.template_name)
        certificate_reject_topic = "$aws/certificates/create/json/rejected"
        
        template_accepted_topic = "$aws/provisioning-templates/{}/provision/json/accepted".format(self.template_name)
        certificate_accepted_topic = "$aws/certificates/create/json/accepted"

        subscribe_topics = [template_reject_topic, certificate_reject_topic, template_accepted_topic, certificate_accepted_topic]

        for mqtt_topic in subscribe_topics:
            print("Subscribing to topic '{}'...".format(mqtt_topic))
            mqtt_topic_subscribe_future, _ = self.primary_MQTTClient.subscribe(
                topic=mqtt_topic,
                qos=mqtt.QoS.AT_LEAST_ONCE,
                callback=self.basic_callback)

            # Wait for subscription to succeed
            mqtt_topic_subscribe_result = mqtt_topic_subscribe_future.result()
            print("Subscribed with {}".format(str(mqtt_topic_subscribe_result['qos'])))


    def get_official_certs(self, callback, isRotation=False):
        """ Initiates an async loop/call to kick off the provisioning flow.

            Triggers:
               on_message_callback() providing the certificate payload
        """
        if isRotation:
            self.template_name = self.rotation_template
            self.isRotation = True

        return asyncio.run(self.orchestrate_provisioning_flow(callback))

    async def orchestrate_provisioning_flow(self,callback):
        # Connect to core with provision claim creds
        self.core_connect()

        # Monitor topics for errors
        self.enable_error_monitor()

        # Make a publish call to topic to get official certs
        #self.primary_MQTTClient.publish("$aws/certificates/create/json", "{}", 0)

        self.primary_MQTTClient.publish(
            topic="$aws/certificates/create/json",
            payload="{}",
            qos=mqtt.QoS.AT_LEAST_ONCE)
        time.sleep(1)

        # Wait the function return until all callbacks have returned
        # Returned denoted when callback flag is set in this class.
        while not self.callback_returned:
            await asyncio.sleep(0)

        return callback(self.message_payload)



    def on_message_callback(self, payload):
        """ Callback Message handler responsible for workflow routing of msg responses from provisioning services.
        
        Arguments:
            payload {bytes} -- The response message payload.
        """
        json_data = json.loads(payload)
        
        # A response has been recieved from the service that contains certificate data. 
        if 'certificateId' in json_data:
            self.logger.info('##### SUCCESS. SAVING KEYS TO DEVICE! #####')
            print('##### SUCCESS. SAVING KEYS TO DEVICE! #####')
            self.assemble_certificates(json_data)
        
        # A response contains acknowledgement that the provisioning template has been acted upon.
        elif 'deviceConfiguration' in json_data:
            if self.isRotation:
                self.logger.info('##### ACTIVATION COMPLETE #####')
                print('##### ACTIVATION COMPLETE #####')
            else:
                self.logger.info('##### CERT ACTIVATED AND THING {} CREATED #####'.format(json_data['thingName']))
                print('##### CERT ACTIVATED AND THING {} CREATED #####'.format(json_data['thingName']))

            self.validate_certs()
        elif 'service_response' in json_data:
            self.logger.info(json_data)
            print('##### SUCCESSFULLY USED PROD CERTIFICATES #####')
        else:
            self.logger.info(json_data)

    def assemble_certificates(self, payload):
        """ Method takes the payload and constructs/saves the certificate and private key. Method uses
        existing AWS IoT Core naming convention.
        
        Arguments:
            payload {string} -- Certifiable certificate/key data.

        Returns:
            ownership_token {string} -- proof of ownership from certificate issuance activity.
        """
        ### Cert ID 
        cert_id = payload['certificateId']
        self.new_key_root = cert_id[0:10]

        self.new_cert_name = '{}-certificate.pem.crt'.format(self.new_key_root)
        ### Create certificate
        f = open('{}/{}'.format(self.secure_cert_path, self.new_cert_name), 'w+')
        f.write(payload['certificatePem'])
        f.close()
        

        ### Create private key
        self.new_key_name = '{}-private.pem.key'.format(self.new_key_root)
        f = open('{}/{}'.format(self.secure_cert_path, self.new_key_name), 'w+')
        f.write(payload['privateKey'])
        f.close()

        ### Extract/return Ownership token
        self.ownership_token = payload['certificateOwnershipToken']
        
        # Register newly aquired cert
        self.register_thing(self.unique_id, self.ownership_token)
        


    def register_thing(self, serial, token):
        """Calls the fleet provisioning service responsible for acting upon instructions within device templates.
        
        Arguments:
            serial {string} -- unique identifer for the thing. Specified as a property in provisioning template.
            token {string} -- The token response from certificate creation to prove ownership/immediate possession of the certs.
            
        Triggers:
            on_message_callback() - providing acknowledgement that the provisioning template was processed.
        """
        if self.isRotation:
            self.logger.info('##### VALIDATING EXPIRY & ACTIVATING CERT #####')
            print('##### VALIDATING EXPIRY & ACTIVATING CERT #####')
        else:
            self.logger.info('##### CREATING THING ACTIVATING CERT #####')
            print('##### CREATING THING ACTIVATING CERT #####')
                
        register_template = {"certificateOwnershipToken": token, "parameters": {"SerialNumber": serial}}
        
        #Register thing / activate certificate
        self.primary_MQTTClient.publish(
            topic="$aws/provisioning-templates/{}/provision/json".format(self.template_name),
            payload=json.dumps(register_template),
            qos=mqtt.QoS.AT_LEAST_ONCE)
        time.sleep(2)

    def validate_certs(self):
        """Responsible for (re)connecting to IoTCore with the newly provisioned/activated certificate - (first class citizen cert)
        """
        self.logger.info('##### CONNECTING WITH OFFICIAL CERT #####')
        print('##### CONNECTING WITH OFFICIAL CERT #####')
        self.cert_validation_test()
        self.new_cert_pub_sub()
        print("##### ACTIVATED AND TESTED CREDENTIALS ({}, {}). #####".format(self.new_key_name, self.new_cert_name))
        print("##### FILES SAVED TO {} #####".format(self.secure_cert_path))

    def cert_validation_test(self):
        event_loop_group = io.EventLoopGroup(1)
        host_resolver = io.DefaultHostResolver(event_loop_group)
        client_bootstrap = io.ClientBootstrap(event_loop_group, host_resolver)

        self.test_MQTTClient = mqtt_connection_builder.mtls_from_path(
            endpoint=self.iot_endpoint,
            cert_filepath="{}/{}".format(self.secure_cert_path, self.new_cert_name),
            pri_key_filepath="{}/{}".format(self.secure_cert_path, self.new_key_name),
            client_bootstrap=client_bootstrap,
            ca_filepath="{}/{}".format(self.secure_cert_path, self.root_cert),
            client_id=self.unique_id + "-Prod",
            clean_session=False,
            keep_alive_secs=6)
        
        print("Connecting with Prod certs to {} with client ID '{}'...".format(self.iot_endpoint, self.unique_id + "-Prod"))
        connect_future = self.test_MQTTClient.connect()
        # Future.result() waits until a result is available
        connect_future.result()
        print("Connected with Prod certs!")

    def basic_callback(self, topic, payload, **kwargs):
        print("Received message from topic '{}': {}".format(topic, payload))
        self.message_payload = payload
        self.on_message_callback(payload)

        if topic == "openworld":
            # Finish the run successfully
            print("Successfully provisioned")
            self.callback_returned = True
        elif (topic == "$aws/provisioning-templates/{}/provision/json/rejected".format(self.template_name) or
            topic == "$aws/certificates/create/json/rejected"):
            print("Failed provisioning")
            self.callback_returned = True

    def new_cert_pub_sub(self):
        """Method testing a call to the 'openworld' topic (which was specified in the policy for the new certificate)
        """

        new_cert_topic = "openworld"
        print("Subscribing to topic '{}'...".format(new_cert_topic))
        mqtt_topic_subscribe_future, _ = self.test_MQTTClient.subscribe(
            topic=new_cert_topic,
            qos=mqtt.QoS.AT_LEAST_ONCE,
            callback=self.basic_callback)

        # Wait for subscription to succeed
        mqtt_topic_subscribe_result = mqtt_topic_subscribe_future.result()
        print("Subscribed with {}".format(str(mqtt_topic_subscribe_result['qos'])))

        self.test_MQTTClient.publish(
            topic="openworld",
            payload=json.dumps({"service_response": "##### RESPONSE FROM PREVIOUSLY FORBIDDEN TOPIC #####"}),
            qos=mqtt.QoS.AT_LEAST_ONCE)


