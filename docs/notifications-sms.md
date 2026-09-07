# SMS notifications

ITAdmin sends SMS through one **active** provider at a time, chosen in
**Settings → Notification Settings → SMS**. Enabling one provider disables the others.

## Providers

| Provider | Key | Use it when |
| --- | --- | --- |
| Custom HTTP | `custom-http` | Any gateway reachable with a single templated HTTP request. You define method, content type, auth, body template and success criteria. |
| Teknomart SMS | `teknomart` | app.teknomart.com.tr. Basic auth over HTTPS to `{baseUrl}/sms/create-otp` (OTP) or `{baseUrl}/sms/create` (Single). |

### Teknomart settings

- **Base URL** — scheme and port included, e.g. `https://api.teknomart.com.tr:9588`.
- **API username / password** — Basic-auth credentials (`Authorization: Basic base64(user:pass)`), stored encrypted; never returned to the browser.
- **Default SMS kind** — `Single` or `Otp`; used when a notification template does not pick its own.
- **Single package title** — 5–50 characters, required whenever Single is reachable; unused for OTP.
- **Encoding** — 0 default, 1 Turkish, 2 UTF-8. **Validity** — OTP 3–6, Single 60–1440 minutes; 0 leaves the provider default.
- **Commercial** — marks sends as commercial so Teknomart runs the İYS check.
- **Delivery report webhook** — optional URL Teknomart POSTs package/recipient status to.

The `{data:{pkgID},err:null}` success envelope is recorded as the provider summary; an
`err` object surfaces as `Teknomart: <message> [<code>]` (e.g. `ERR_USER_CREDIT_REQUIRED`).

## Per-notification OTP vs Single

Each SMS **notification template** (Settings → Notification Settings → Templates) has an
**SMS kind** field:

- **Provider default** — follow the provider's configured default.
- **OTP** — force `/sms/create-otp`.
- **Single** — force `/sms/create`.

The chosen kind is copied onto the outbox row at enqueue time and passed to the provider
when the message is sent. Providers without distinct endpoints (Custom HTTP) ignore it.

## Testing

The SMS tab's **Send test** button sends a real message through the saved, enabled provider.
Save first — it uses the stored settings, not the unsaved form.
