# Enrollment Sequencediagram
Below you can find the sequence diagram code which you can use into https://sequencediagram.org/ website


## code reppresentation of sequence diagram

    title Enrollment App flow

    actor Service Engineer
    participant Enrollment App
    participant TPM
    participant AAD
    participant DPS

    Service Engineer->Enrollment App:Service Engineer start the\nEnrollment Application

    Enrollment App->TPM:Enrollment App retreive the\nPublic Endorsement Key from TPM

    TPM-->Enrollment App:Public Endorsement Key
    Enrollment App->AAD:Initiate Device Code Flow

    opt Device Code auth flow

    Enrollment App->Service Engineer:Authentication required\nto connect to DPS

    Service Engineer->AAD:User Authenticate with Device Code Flow

    AAD-->Service Engineer:Auth done

    AAD-->Enrollment App:Access Token

    Enrollment App<--Service Engineer:Auth Completed

    Enrollment App->DPS:DPS Client leverage Access Token for auth

    end

    opt DefaultCredential approach

    Enrollment App->DPS:DPS Client leverage AZ CLI authentication for auth

    end

    Enrollment App->DPS:Create Individual Enrollment for the device\nwith the TPM Public Endorsement Key
