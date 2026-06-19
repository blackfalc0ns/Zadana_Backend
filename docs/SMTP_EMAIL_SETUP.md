# SMTP email setup

The API sends email through SMTP and no longer depends on Resend.

## API environment variables

```text
Email__FromEmail=support@zadna0.com
Email__FromName=Zadna
Email__SupportEmail=support@zadna0.com
Email__HelloEmail=hello@zadna0.com
Email__InfoEmail=info@zadna0.com
Email__ContactEmail=contact@zadna0.com

Email__Smtp__Host=mail.zadna0.com
Email__Smtp__Port=587
Email__Smtp__Security=StartTls
Email__Smtp__Username=support@zadna0.com
Email__Smtp__Password=<smtp-password>
Email__Smtp__TimeoutSeconds=30
Email__Smtp__RequireAuthentication=true
```

Use port 587 with `StartTls` when the mail provider supports it. For implicit
TLS on port 465, set `Security=SslOnConnect`.

The API reuses one authenticated SMTP connection and serializes sends because
SMTP clients are not thread-safe. Failed delivery is still returned to the
caller, so OTP flows do not report success before the mail server accepts the
message.

## DNS deliverability

Sending from the API does not by itself prevent spam placement. Configure:

- SPF authorizing the actual outgoing SMTP server/IP.
- DKIM signing in the mail-server control panel.
- DMARC, starting with `p=none` while reviewing reports.
- PTR/reverse DNS for a dedicated outgoing IP, when self-hosting SMTP.
- Matching HELO hostname, forward DNS, and TLS certificate.

Do not run an open relay. Require SMTP authentication and block public relay
for unauthenticated users. Warm up a new IP gradually and remove bounced or
invalid recipients.

## Security

Store the SMTP password only as a server environment variable. Remove the old
Resend API key and rotate it because it is no longer used.
