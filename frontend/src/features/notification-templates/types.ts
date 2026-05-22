export type NotificationTemplateListItem = {
  id: string;
  moduleKey: string;
  eventKey: string;
  channel: string;
  name: string;
  isEnabled: boolean;
  updatedAt: string | null;
};

export type NotificationTemplate = {
  id: string;
  moduleKey: string;
  eventKey: string;
  channel: string;
  name: string;
  isEnabled: boolean;
  subjectTemplate: string | null;
  bodyTemplate: string;
  description: string | null;
  createdAt: string;
  createdBy: string | null;
  updatedAt: string | null;
  updatedBy: string | null;
};

export type SaveNotificationTemplateRequest = {
  moduleKey: string;
  eventKey: string;
  channel: string;
  name: string;
  isEnabled: boolean;
  subjectTemplate?: string | null;
  bodyTemplate: string;
  description?: string | null;
};
