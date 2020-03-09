## Device Fleet Provisioning with AWS IoTCore

It can often be difficult to manage the secure provisioning of myriad IoT devices in the field. This process can often involve invasive workflow measures, qualified personnel, secure handling of sensitive information, and management of dispensed credentials. Through IoT Core, AWS Fleet Provisioning provides a service oriented, api approach to managing credentials. To learn more about these rich capabilities, read here: https://docs.aws.amazon.com/iot/latest/developerguide/iot-provision.html

To aid in the adoption and utilization of the functionality mentioned above, this repo provides a reference client to illustrate how device(s) might interact with the provided services to deliver the desired experience. Specifically, the client demonstrates how a common "bootstrap" certificate (placed on n devices) can, upon a first-run experience:

1. Connect to IoTCore with stringent bootstrap credentials
1. Obtain a unique private key and "production" certificate
1. Present proof of ownership of the production credentials
1. Prompt the execution of a provisioning template (custom provisioning logic)
1. Rotate the certificates (decommission bootstrap, promote new cert)
1. Test the rights of the newly aquired certificate.


## Dependencies of the solution


### At present, the beta ONLY works in US-EAST-1!!

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
1. Still in the IoT Core console, make sure you are in us-east-1.
1. Select *Onboard* and then *Fleet Provisioning Templates* and finally, *Create*.
1. Name your provisioning template (e.g. - birthing_template). Remember this name!
1. Create or associate a basic IoT Role with this template. (at least - AWSIoTThingsRegistration, AWSIoTLogging)
1. *Leave optional settings unchecked for now.*
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
#### Run solution
1. > python3 main.py

If the solution runs without error, you should notice the new certificates saved in the same directory as the bootstrap certs. You will also notice the creation of THINGS in the IoT Registry that are activated. As this solution is only meant to demo the solution, each subsequent run will use the original bootstrap cert to request new credentials, and therfore also create another thing. Thing names are based on a dynamically generated serial number presented in the code.


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

#### Sample provisioning template JSON
``` json
{
  "Parameters": {
    "SerialNumber": {
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
        "PolicyName": "full_citizen_role"
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
        "AttributePayload": {},
        "ThingGroups": [],
        "ThingName": {
          "Fn::Join": [
            "",
            [
              "born_",
              {
                "Ref": "SerialNumber"
              }
            ]
          ]
        }
      },
      "Type": "AWS::IoT::Thing"
    }
  },
  "DeviceConfiguration": {}
}
```


## License

This library is licensed under the MIT-0 License. See the LICENSE file.

