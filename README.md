# Agent provisioning with local DPS cache
This Repo contains a simple example on how to create an IoT Agent which will connect to IoT Hub using Device Provisioning Service and cache locally the Provisioning Details to avoid re-provision on every restart the Agent.

As attestation mechanism i choose TPM chip which is a secure way to enroll a device into Device Provisioning Service.


## Key Takeaways
Following the DPS recommendation to [not provision the device on every reboot](https://docs.microsoft.com/azure/iot-dps/how-to-reprovision#send-a-provisioning-request-from-the-device) in this repo you can find an example of implementation of an Agent device which leverage a local cache for the provisioning details.

In this repo you can also find a sample application to manage the initial enrollment of the agent and a CLI app to send a Direct Method call to reprovision the device in case you need to change the IoT Hub to which the agent need to connect to (e.g. migration from a single tenant to a multi-tenant approach or as a disaster recovery process).

You will find a simple example of Custom Allocation Policy function you can eventually use within DPS during the Registration.

The Enrollment App is using [Device Code flow](https://docs.microsoft.com/azure/active-directory/develop/v2-oauth2-device-code) to authenticate toward the DPS service endpoint for the very first time showing a QR Code with the link to the AAD Device Code flow Authentication page:

<img width="883" alt="image" src="https://user-images.githubusercontent.com/45007019/168384168-6d991a20-697d-415e-8996-4d3128a5a3ef.png">

### Enrollment alternatives

>You can think about a different approach to implement the initial registration like calling an API from an installer tool and then setting up a validation process in the cloud to confirm the trust and configure the enrollment key into Device Provisioning Services asyncronously. 
In this case the Agent App should consider that the Registration could fail for a long period of time after the initial boot.

## Agent app Sequence Diagram

Below you can find the [Sequence diagram](./agent-sequencediagram.md) followed by the Agent app.

![image](https://user-images.githubusercontent.com/45007019/166897728-eb0e2e65-56cd-40b3-8346-e40d311d1459.png)

## Enrollment app Sequence Diagram

Below you can find the [Sequence diagram](./enrollment-sequencediagram.md) followed by the Enrollment app.

![image](https://user-images.githubusercontent.com/45007019/166897600-f7d11826-1710-4781-9448-796e25f5646a.png)





