# Agent Sequencediagram
Below you can find the sequence diagram code which you can use into https://sequencediagram.org/ website


## code reppresentation of sequence diagram

    title Connecting to IoT Hub with local DPS cache

    #actor Service Engineer
    #participant Registering Application

    participant Agent
    participant Local Cache
    participant DPS
    participant IoT Hub

    Agent->Local Cache: Agent check Provisioning\ndetails from local cache

    Local Cache-->Agent: Local Cache returns\n(if available) provisionig details (IoT Hub hostname)

    alt No Cached provisioning details


    loop  retry policy with exponential backoff and jitter in case of failure

    Agent->(2)DPS:Agent connects to DPS and try Register itself

    DPS-->Agent:Registration response

    end 

    Agent->Local Cache:Cache Provisioning Details
    Local Cache-->Agent:Caching completed.

    end

    note over Agent,IoT Hub:In that moment the Agent should be registered to an IoT Hub


    Agent->IoT Hub:Agent connects to the allocated IoT Hub
    IoT Hub-->Agent:Connection Accepted
    note over Agent,IoT Hub:Now Agent is connected to IoT Hub

    group In case of on demand reprovisioning needs

    IoT Hub->Agent:Direct Method call to wipe provisioning details and reprovision

    Agent->Local Cache:Wipe Provisioning Details
    Local Cache-->Agent:Wipe executed.

    Agent-->IoT Hub:Wipe executed, reprovisioning.

    Agent->Agent:Restart connection process

    end
