# ------------------------------------------------------------------------------
# Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
# SPDX-License-Identifier: MIT-0 
# -----------------------------------------
# Consuming sample, demonstrating how a device process would leverage the provisioning class.
#  The handler makes use of the asycio library and therefore requires Python 3.7.
#
#  Prereq's: 
#      1) A provisioning claim certificate has been cut from AWSIoT.
#	   2) A restrictive "birth" policy has been associated with the certificate.
#      3) A provisioning template was created to manage the activities to be performed during new certificate activation.
#	   4) The claim certificate was placed securely on the device fleet and shipped to the field. (along with root/ca and private key)
#  
#  Execution:
#      1) The paths to the certificates, and names of IoTCore endpoint and provisioning template are set in config.ini (this project)  
#	   2) A device boots up and encounters it's "first run" experience and executes the process (main) below.
# 	   3) The process instatiates a handler that uses the bootstrap certificate to connect to IoTCore.
#	   4) The connection only enables calls to the Foundry provisioning services, where a new certificate is requested.
#      5) The certificate is assembled from the response payload, and a foundry service call is made to activate the certificate.
#	   6) The provisioning template executes the instructions provided and the process rotates to the new certificate.
#      7) Using the new certificate, a pub/sub call is demonstrated on a previously forbidden topic to test the new certificate.
#      8) New certificates are saved locally, and can be stored/consumed as the application deems necessary.
#
#
# Initial version - Raleigh Murch, AWS
# email: murchral@amazon.com
# ------------------------------------------------------------------------------

from provisioning_handler import ProvisioningHandler
from utils.config_loader import Config
from pyfiglet import Figlet


#Set Config path
CONFIG_PATH = 'config.ini'

config = Config(CONFIG_PATH)
config_parameters = config.get_section('SETTINGS')
secure_cert_path = config_parameters['SECURE_CERT_PATH']
bootstrap_cert = config_parameters['CLAIM_CERT']

# Demo Theater
f = Figlet(font='slant')
print(f.renderText('      F l e e t'))
print(f.renderText('Provisioning'))
print(f.renderText('----------'))

# Provided callback for provisioning method feedback.
def callback(payload):
    print(payload)

# Used to kick off the provisioning lifecycle, exchanging the bootstrap cert for a
# production certificate after being validated by a provisioning hook lambda.
#
# isRotation = True is used to rotate from one production certificate to a new production certificate. 
# Certificates signed by AWS IoT Root CA expire on 12/31/2049. Security best practices
# urge frequent rotation of x.509 certificates and this method (used in conjunction with
# a cloud cert management pattern) attempts to make cert exchange easy.
def run_provisioning(isRotation):

    provisioner = ProvisioningHandler(CONFIG_PATH)

    if isRotation:
        provisioner.get_official_certs(callback, isRotation=True)  
    else:
        #Check for availability of bootstrap cert 
        try:
             with open("{}/{}".format(secure_cert_path, bootstrap_cert)) as f:
                # Call super-method to perform aquisition/activation
                # of certs, creation of thing, etc. Returns general
                # purpose callback at this point.
                # Instantiate provisioning handler, pass in path to config
                provisioner.get_official_certs(callback)

        except IOError:
            print("### Bootstrap cert non-existent. Official cert may already be in place.")

if __name__ == "__main__":
    run_provisioning(isRotation=False)

    

		
	