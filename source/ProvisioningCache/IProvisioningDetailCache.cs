namespace ProvisioningCache
{
    public interface IProvisioningDetailCache
    {
        ProvisioningResponse GetProvisioningDetailResponseFromCache(string registrationId);

        void SetProvisioningDetailResponse(string registrationId, ProvisioningResponse provisioningDetails);
    }
}