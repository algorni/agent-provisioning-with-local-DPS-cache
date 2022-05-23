namespace ProvisioningCache
{
    public interface IProvisioningDetailCache
    {
        Task<ProvisioningResponse> GetProvisioningDetailResponseFromCache(string registrationId);

        Task SetProvisioningDetailResponse(string registrationId, ProvisioningResponse provisioningDetails);

        Task ClearProvisioningDetail(string registrationId);
    }
}