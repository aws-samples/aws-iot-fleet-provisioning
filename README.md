## Device Fleet Provisioning with AWS IoTCore

*Updates*:
 - Now with the ability to respond to cert rotation requests. When the device has been informed it needs to rotate certificates, simply set an additional (optional) attribute isRotation = True. This update is used in conjunction with a cert_rotation policy specified below. This solution relies on setting a cert_issuance date in the registry when the certificate is registered. This is handled by the provisioning template. Once the device is notified, it can process the rotation through setting the flag below.
 
```
provisioner.get_official_certs(callback, isRotation=True)
```
-------------------------------------------

It can often be difficult to manage the secure provisioning of myriad IoT devices in the field. This process can often involve invasive workflow measures, qualified personnel, secure handling of sensitive information, and management of dispensed credentials. Through IoT Core, AWS Fleet Provisioning provides a service oriented, api approach to managing credentials. To learn more about these rich capabilities, read here: https://docs.aws.amazon.com/iot/latest/developerguide/iot-provision.html

To aid in the adoption and utilization of the functionality mentioned above, this repo provides a reference client to illustrate how device(s) might interact with the provided services to deliver the desired experience. Specifically, the client demonstrates how a common "bootstrap" certificate (placed on n devices) can, upon a first-run experience:

1. Connect to IoTCore with stringent bootstrap credentials
1. Obtain a unique private key and "production" certificate
1. Present proof of ownership of the production credentials
1. Prompt the execution of a provisioning template (custom provisioning logic)
1. Rotate the certificates (decommission bootstrap, promote new cert)
1. Test the rights of the newly acquired certificate.


## Dependencies of the solution
* Intended to be compatible with AWS Greengrass ... this solution depends on a python library (asyncio) which is __only available w/ python 3.7 and above.__ Please ensure your solution has at least this version.

* A .NET Core port of the reference client application is available within the [dotnet-core](/dotnet-core) folder - this does not currently support certificate rotation feature available on the Python version.

* With any connection to IoT Core, you will require the addition of a root CA. We have included a root ca in the repo for convenience but we can't guarantee it will remain current. You can download/replace the contents from the latest contents here: https://www.amazontrust.com/repository/AmazonRootCA1.pem

* It is recommended to use the general sample provisioning template below if you want the provisioning template to create a thing in IoT Core, Activate the cert, etc. Specifically, ensure the THING node attributes are included in YOUR template if you don't use it verbatim.

In order to run the client solution seamlessly you must configure dependencies in 2 dimensions:
AWS Console / Edge Device

### On the AWS Console:
#### Create a common bootstrap certificate.
1. Go to the *IoT Core* Service, and in the menu on the left select *Secure* and finally, *Certificates*.
1. Select *Create* to create your common bootstrap certificates.
1. Choose *One Click Certificate Creation* (This will create your bootstrap cert to be placed on all devices)
1. Download and store certificates.
1. ! Don't forget to download a root.ca and select the button to *ACTIVATE* your certificate on the same screen.

#### Create Provisioning Template / Attach Policies
1. In console, select *Onboard* and then *Fleet Provisioning Templates* and finally, *Create*.
1. Name your provisioning template (e.g. - birthing_template). Remember this name!
1. Create or associate a basic IoT Role with this template. (at least - AWSIoTThingsRegistration)
1. Select "Use the AWS IoT registry ..." to ensure the sample code works appropriately as it creates things here.
1. Select Next
1. Create or select the policy that you wish fully provisioned devices to have. (see sample open policy below)
1. Select Create Template
1. Select the bootstrap certificate you created above and select *Attach Policy*.
1. Ignore the section on  Create IAM role to Provision devices, and select *Enable template*.
1. Now select *close* to return to the console.

### On the Edge device

#### Basic python hygiene
1. Clone the aws-iot-fleet-provisioning repo to your edge device.
1. Consider running the solution in a python virtual environment.
1. Install python dependencies: ```pip3 install -r requirements.txt``` (requirements.txt located in solution root)

#### Solution setup
1. Take your downloaded bootstrap credentials (including root.ca) and securely store them on your device.
1. Find config.ini within the solution and configure the below parameters:
```python
SECURE_CERT_PATH = PATH/TO/YOUR/CERTS
ROOT_CERT = root.ca.pem
CLAIM_CERT = xxxxxxxxxx-certificate.pem.crt
SECURE_KEY = xxxxxxxxxx-private.pem.key
IOT_ENDPOINT = xxxxxxxxxx-ats.iot.us-east-1.amazonaws.com
PROVISIONING_TEMPLATE_NAME = my_template (e.g. - birthing_template)
```
#### Run solution (may need to use *sudo* if storing certificates in a protected dir)
1. > python3 main.py

If the solution runs without error, you should notice the new certificates saved in the same directory as the bootstrap certs. You will also notice the creation of THINGS in the IoT Registry that are activated. As this solution is only meant to demo the solution, each subsequent run will use the original bootstrap cert to request new credentials, and therefore also create another thing. Thing names are based on a dynamically generated serial number presented in the code.

### See below for examples of necessary artifacts as part of this solution:

#### Sample "birth_policy" applied to a bootstrap certificate with permissions limited only to provisioning api's.
Note: If using the fleet provisioning feature in the console, this policy will be applied to the certificate automatically.
Also, if you intend to copy/paste the below policy note the arn's and change the region/account number as appropriate.
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "iot:Connect"
      ],
      "Resource": "*"
    },
    {
      "Effect": "Allow",
      "Action": [
        "iot:Publish",
        "iot:Receive"
      ],
      "Resource": [
        "arn:aws:iot:us-east-1:XXXXXXXXXXXX:topic/$aws/certificates/create/*",
        "arn:aws:iot:us-east-1:XXXXXXXXXXXX:topic/$aws/provisioning-templates/birthing_template/provision/*"
      ]
    },
    {
      "Effect": "Allow",
      "Action": "iot:Subscribe",
      "Resource": [
        "arn:aws:iot:us-east-1:XXXXXXXXXXXX:topicfilter/$aws/certificates/create/*",
        "arn:aws:iot:us-east-1:XXXXXXXXXXXX:topicfilter/$aws/provisioning-templates/birthing_template/provision/*"
      ]
    }
  ]
}




#### Sample Policy for fully provisioned devices - aptly named 'full_citizen_role'
``` json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "iot:Publish",
        "iot:Subscribe",
        "iot:Connect",
        "iot:Receive"
      ],
      "Resource": [
        "*"
      ]
    },
    {
      "Effect": "Allow",
      "Action": [
        "iot:GetThingShadow",
        "iot:UpdateThingShadow",
        "iot:DeleteThingShadow"
      ],
      "Resource": [
        "*"
      ]
    },
    {
      "Effect": "Allow",
      "Action": [
        "greengrass:*"
      ],
      "Resource": [
        "*"
      ]
    }
  ]
}
```

#### Sample provisioning hook where you validate the request before activating a certificate
```
import json
from datetime import date

provision_response = {
    'allowProvisioning': False,
    "parameterOverrides": {"CertDate": date.today().strftime("%m/%d/%y")}
}


def handler(event, context):

    ########################
    ## Stringent validation against internal API's/DB etc to validate the request before proceeding
    ##
    ## if event['parameters']['SerialNumber'] = "approved by company CSO":
    ##     provision_response["allowProvisioning"] = True
    #####################
    
  
    return provision_response
```

#### Sample provisioning template JSON
``` json
{
  "Parameters": {
    "CertDate": {
      "Type": "String"
    },
    "deviceId": {
      "Type": "String"
    },
    "AWS::IoT::Certificate::Id": {
      "Type": "String"
    }
  },
  "Resources": {
    "certificate": {
      "Properties": {
        "CertificateId": {
          "Ref": "AWS::IoT::Certificate::Id"
        },
        "Status": "Active"
      },
      "Type": "AWS::IoT::Certificate"
    },
    "policy": {
      "Properties": {
        "PolicyName": "fleetprov_prod_template"
      },
      "Type": "AWS::IoT::Policy"
    },
    "thing": {
      "OverrideSettings": {
        "AttributePayload": "MERGE",
        "ThingGroups": "DO_NOTHING",
        "ThingTypeName": "REPLACE"
      },
      "Properties": {
        "AttributePayload": {
          "cert_issuance": {
            "Ref": "CertDate"
          }
        },
        "ThingGroups": [],
        "ThingName": {
          "Ref": "deviceId"
        }
      },
      "Type": "AWS::IoT::Thing"
    }
  },
  "DeviceConfiguration": {
  }
}


```

#### Sample Cert Rotation Provisioning Template. Used to activate a new AWS IoT Certificate, and update the cert_issuance attribute in the registry.
```
{
  "Parameters": {
    "SerialNumber": {
      "Type": "String"
    },
    "CertDate": {
      "Type": "String"
    },
    "AWS::IoT::Certificate::Id": {
      "Type": "String"
    }
  },
  "Resources": {
    "certificate": {
      "Properties": {
        "CertificateId": {
          "Ref": "AWS::IoT::Certificate::Id"
        },
        "Status": "Active"
      },
      "Type": "AWS::IoT::Certificate"
    },
    "policy": {
      "Properties": {
        "PolicyName": "fleetprov_prod_template"
      },
      "Type": "AWS::IoT::Policy"
    },
    "thing": {
      "OverrideSettings": {
        "AttributePayload": "REPLACE",
        "ThingGroups": "REPLACE",
        "ThingTypeName": "REPLACE"
      },
      "Properties": {
        "AttributePayload": {
          "cert_issuance": {
            "Ref": "CertDate"
          }
        },
        "ThingGroups": [],
        "ThingName": {
          "Ref": "SerialNumber"
        }
      },
      "Type": "AWS::IoT::Thing"
    }
  }
}
```

#### Sample AWS Lambda function used as a provisioning hook for cert rotation requests.
```
import json
import boto3
from datetime import date, timedelta

client = boto3.client('iot')
endpoint = boto3.client('iot-data')

#used to validate device actually needs a new cert
CERT_ROTATION_DAYS = 360

#validation check date for registry query
target_date = date.today()-timedelta(days=CERT_ROTATION_DAYS)
target_date = target_date.strftime("%Y%m%d")

#Set up payload with new cert issuance date
provision_response = {'allowProvisioning': False, "parameterOverrides": {
    "CertDate": date.today().strftime("%Y%m%d")}}


def handler(event, context):

    # Future log Cloudwatch logs
    print("Received event: " + json.dumps(event, indent=2))

    thing_name = event['parameters']['SerialNumber']
    response = client.describe_thing(
    thingName=thing_name)
 
    try:
      #Cross reference ID of requester with entry in registry to ensure device needs a rotation.
      if int(response['attributes']['cert_issuance']) < int(target_date):
        provision_response["allowProvisioning"] = True
    except:
      provision_response["allowProvisioning"] = False

    return provision_response
```

#### Sample Lambda used by Cloudwatch as a monitoring agent to notify devices when they're due for a cert rotation
```
import json
import boto3
from datetime import date, timedelta

client = boto3.client('iot')
endpoint = boto3.client('iot-data')

#Set Cert Rotation Interval
CERT_ROTATION_DAYS = 360

#Check for certificate expiry due in next 2 weeks.
target_date = date.today()-timedelta(days=CERT_ROTATION_DAYS)

#Convert to numeric format
target_date = target_date.strftime("%Y%m%d")


def lambda_handler(event, context):
  
  response = client.search_index(
    queryString='attributes.cert_issuance<{}'.format(target_date),
    maxResults=100)
 
  for thing in response['things']:
    endpoint.publish(
      topic='cmd/{}'.format(thing['thingName']),
      payload='{"msg":"rotate_cert"}'
      )
  
  return {
    'things': response['things']
  }
```




## License

This library is licensed under the MIT-0 License. See the LICENSE file.

