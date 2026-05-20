namespace SasPortal.Api.Contracts.AdManagement;

public sealed record AdUpnSuffixItemResponse(string Value, string Source);

public sealed record AdUpnSuffixesResponse(
    IReadOnlyList<AdUpnSuffixItemResponse> Items,
    string? Warning);
