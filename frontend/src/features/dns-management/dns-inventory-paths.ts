export const DNS_ZONES_PATH = "/dns-management/zones";

export const buildDnsZoneRecordsPath = (zoneSnapshotId: string) =>
  `${DNS_ZONES_PATH}/${encodeURIComponent(zoneSnapshotId)}/records`;
