## Fleet Provisioning from a .NET Core Client

1. Follow the instructions in the [root-level README](../README.md) to configure the dependencies on the AWS Console.
2. Refer [this section](../README.md#see-below-for-examples-of-necessary-artifacts-as-part-of-this-solution) for the samples on IoT policies and provisioning templates.
3. Clone the repository to the edge device.
4. Run `dotnet restore dotnet-core/AWS.IoT.FleetProvisioning/AWS.IoT.FleetProvisioning.csproj` to install the NuGet dependencies.
5. Place the downloaded bootstrap credentials on your device (at the `dotnet-core/AWS.IoT.FleetProvisioning/Certs/` folder) beside the `root.ca.pem` file.
6. Update `dotnet-core/AWS.IoT.FleetProvisioning/appsettings.json` to configure the below parameters:
```json
  "settings": {
    // absolute path to the folder containing your certificates
    "secureCertificatePath": "/PATH/TO/CERTS",
    // names for root certificate, provisioning claim certificate, and private key.
    "rootCertificate": "root.ca.pem",
    "claimCertificate": "bootstrap-certificate.pem.crt",
    "claimCertificateKey": "bootstrap-private.pem.key",
    // IoT Data:ATS Endpoint (run `aws iot describe-endpoint --endpoint-type iot:Data-ATS` to get this value)
    "iotEndpoint": "xxxxxxxxxxxxxx-ats.iot.{REGION}.amazonaws.com",
    // name for the provisioning template that was created in IoT Core
    "provisioningTemplate": "Provisioning-Template"
  }
```
7. Change into the project directory: `cd dotnet-core/AWS.IoT.FleetProvisioning`
8. Run the solution: `dotnet run`

If the solution runs without errors, you will notice that the new certificates are saved in `secureCertificatePath` directory. You will also notice that new "Thing" has been created and activated in [the IoT Registry](https://console.aws.amazon.com/iot/home#/thinghub). As this is only meant to be a demo, each subsequent run will use the original bootstrap cert to request new credentials, and therefore will create new "Things". Thing names are based on dynamically generated "serial numbers" (which are just new `Guid`s) as can be seen in the code.