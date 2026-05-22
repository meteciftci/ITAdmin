export type NotificationTemplateCatalogVariable = {
  key: string;
  example: string | null;
};

export type NotificationTemplateCatalogEvent = {
  key: string;
  supportedChannels: string[];
  variables: NotificationTemplateCatalogVariable[];
};

export type NotificationTemplateCatalogModule = {
  key: string;
  events: NotificationTemplateCatalogEvent[];
};

export type NotificationTemplateCatalog = {
  modules: NotificationTemplateCatalogModule[];
};
