import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { useTranslation } from "react-i18next";

export type LdapFormValues = {
  name: string;
  host: string;
  port: string;
  baseDn: string;
  userSearchBase: string;
  userSearchFilter: string;
  bindUserName: string;
  bindUserDomain: string;
  bindPassword: string;
  description: string;
};

type LdapSettingsFormProps = {
  values: LdapFormValues;
  fieldErrors: Partial<Record<keyof LdapFormValues, string>>;
  hasBindPassword: boolean;
  readOnly: boolean;
  savePending: boolean;
  canSave: boolean;
  onChange: <K extends keyof LdapFormValues>(field: K, value: LdapFormValues[K]) => void;
  onSave: () => void;
};

export function LdapSettingsForm({
  values,
  fieldErrors,
  hasBindPassword,
  readOnly,
  savePending,
  canSave,
  onChange,
  onSave,
}: LdapSettingsFormProps) {
  const { t } = useTranslation(["settings", "common"]);

  return (
    <div className="space-y-4">
      <div className="grid gap-4 md:grid-cols-2">
        <Field
          label={t("settings:ldap.fields.name")}
          error={fieldErrors.name}
          required
          input={
            <Input
              value={values.name}
              onChange={(event) => onChange("name", event.target.value)}
              readOnly={readOnly}
              aria-invalid={Boolean(fieldErrors.name)}
            />
          }
        />

        <Field
          label={t("settings:ldap.fields.host")}
          error={fieldErrors.host}
          required
          input={
            <Input
              value={values.host}
              onChange={(event) => onChange("host", event.target.value)}
              readOnly={readOnly}
              aria-invalid={Boolean(fieldErrors.host)}
            />
          }
        />

        <Field
          label={t("settings:ldap.fields.port")}
          error={fieldErrors.port}
          required
          input={
            <Input
              type="number"
              min={1}
              max={65535}
              value={values.port}
              onChange={(event) => onChange("port", event.target.value)}
              readOnly={readOnly}
              aria-invalid={Boolean(fieldErrors.port)}
            />
          }
        />

        <div className="space-y-1.5">
          <Label>{t("settings:ldap.fields.security")}</Label>
          <p className="flex min-h-9 items-center text-sm text-muted-foreground">
            {t("settings:ldap.ldapsHelp")}
          </p>
        </div>

        <Field
          label={t("settings:ldap.fields.baseDn")}
          error={fieldErrors.baseDn}
          required
          input={
            <Input
              value={values.baseDn}
              onChange={(event) => onChange("baseDn", event.target.value)}
              readOnly={readOnly}
              aria-invalid={Boolean(fieldErrors.baseDn)}
            />
          }
        />

        <Field
          label={t("settings:ldap.fields.userSearchBase")}
          input={
            <Input
              value={values.userSearchBase}
              onChange={(event) => onChange("userSearchBase", event.target.value)}
              readOnly={readOnly}
            />
          }
        />

        <Field
          label={t("settings:ldap.fields.userSearchFilter")}
          error={fieldErrors.userSearchFilter}
          required
          input={
            <Input
              value={values.userSearchFilter}
              onChange={(event) => onChange("userSearchFilter", event.target.value)}
              readOnly={readOnly}
              aria-invalid={Boolean(fieldErrors.userSearchFilter)}
            />
          }
        />

        <Field
          label={t("settings:ldap.fields.bindUserName")}
          error={fieldErrors.bindUserName}
          required
          input={
            <Input
              value={values.bindUserName}
              onChange={(event) => onChange("bindUserName", event.target.value)}
              readOnly={readOnly}
              aria-invalid={Boolean(fieldErrors.bindUserName)}
            />
          }
        />

        <Field
          label={t("settings:ldap.fields.bindUserDomain")}
          input={
            <Input
              value={values.bindUserDomain}
              onChange={(event) => onChange("bindUserDomain", event.target.value)}
              readOnly={readOnly}
            />
          }
        />

        <Field
          label={t("settings:ldap.fields.bindPassword")}
          input={
            <Input
              type="password"
              value={values.bindPassword}
              onChange={(event) => onChange("bindPassword", event.target.value)}
              readOnly={readOnly}
            />
          }
          helpText={
            <>
              {hasBindPassword ? (
                <span>{t("settings:ldap.bindPasswordConfigured")}</span>
              ) : null}
              <span>{t("settings:ldap.bindPasswordKeepHint")}</span>
            </>
          }
        />
      </div>

      <Field
        label={t("settings:ldap.fields.description")}
        input={
          <Textarea
            value={values.description}
            onChange={(event) => onChange("description", event.target.value)}
            readOnly={readOnly}
          />
        }
      />

      {!readOnly ? (
        <div className="flex justify-end">
          <Button onClick={onSave} disabled={!canSave || savePending}>
            {t("common:actions.save")}
          </Button>
        </div>
      ) : null}
    </div>
  );
}

type FieldProps = {
  label: string;
  input: React.ReactNode;
  error?: string;
  helpText?: React.ReactNode;
  required?: boolean;
};

function Field({ label, input, error, helpText, required }: FieldProps) {
  return (
    <div className="space-y-1.5">
      <Label>
        {label}
        {required ? <span className="text-destructive">*</span> : null}
      </Label>
      {input}
      {error ? <p className="text-xs text-destructive">{error}</p> : null}
      {helpText ? <div className="space-y-1 text-xs text-muted-foreground">{helpText}</div> : null}
    </div>
  );
}
