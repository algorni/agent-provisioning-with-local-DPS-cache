# Agent provisioning with local DPS cache
This Repo contains a simple example on how to cache locally the DPS Provisioning Details to avoid provision on every restart the Agent.

## Key Takeaways
Following the DPS recommendation to [not provision the device on every reboot](https://docs.microsoft.com/azure/iot-dps/how-to-reprovision#send-a-provisioning-request-from-the-device) in this repo you can find an example of implementation of an Agent device which leverage a local cache for the provisioning details.

In this repo you can also find a sample application to manage the initial registration of the device and a CLI app to send a Direct Method call to reprovision the device. 

You will find a simple example of Custom Allocation Policy function.

Both the Registration app and the Reprovisioning Command App is using AAD to authenticate toward the DPS and IoT Hyb service endpoint.
As fallback you can use also [AAD Device Code flow](https://docs.microsoft.com/azure/active-directory/develop/v2-oauth2-device-code). 

## Agent app Sequence Diagram

Below you can find the [Sequence diagram](./agent-sequencediagram.md) followed by the Agent app.

![image](https://user-images.githubusercontent.com/45007019/166897728-eb0e2e65-56cd-40b3-8346-e40d311d1459.png)

## Enrollment app Sequence Diagram

Below you can find the [Sequence diagram](./enrollment-sequencediagram.md) followed by the Enrollment app.

![image](https://user-images.githubusercontent.com/45007019/166897600-f7d11826-1710-4781-9448-796e25f5646a.png)

