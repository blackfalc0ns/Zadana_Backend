# SMTP email setup

The API sends email through the self-hosted mail server (`mail.zadna0.com`).
Do not use Brevo, Resend, or any third-party SMTP relay.

## Production (recommended)

On the API host, apply mailbox credentials with:

```bash
sudo bash deploy/apply-email-env.sh
```

Defaults:

- Host: `mail.zadna0.com`
- Port: `465` (`SslOnConnect`)
- Username / From: `no-reply@zadna0.com`

## API environment variables

```text
Email__FromEmail=no-reply@zadna0.com
Email__FromName=Zadna
Email__SupportEmail=support@zadna0.com
Email__HelloEmail=hello@zadna0.com
Email__InfoEmail=info@zadna0.com
Email__ContactEmail=contact@zadna0.com

Email__Smtp__Host=mail.zadna0.com
Email__Smtp__Port=465
Email__Smtp__Security=SslOnConnect
Email__Smtp__Username=no-reply@zadna0.com
Email__Smtp__Password=<mailbox-password>
Email__Smtp__TimeoutSeconds=30
Email__Smtp__RequireAuthentication=true
```

Deployed services often use the `ZADANA_` prefix (`ZADANA_Email__Smtp__Host`, …).

If your mail server prefers submission on 587 instead of 465, use
`Port=587` and `Security=StartTls`.

The API reuses one authenticated SMTP connection and serializes sends because
SMTP clients are not thread-safe. Failed delivery is still returned to the
caller, so OTP flows do not report success before the mail server accepts the
message.

## DNS deliverability

Sending from the API does not by itself prevent spam placement. Configure:

- SPF authorizing the actual outgoing SMTP server/IP for `zadna0.com`.
- DKIM signing in the mail-server / cPanel control panel.
- DMARC, starting with `p=none` while reviewing reports.
- PTR/reverse DNS for a dedicated outgoing IP.
- Matching HELO hostname, forward DNS, and TLS certificate.

Do not run an open relay. Require SMTP authentication and block public relay
for unauthenticated users. Warm up a new IP gradually and remove bounced or
invalid recipients.

## Security

Store the SMTP password only as a server environment variable. Remove any
leftover Brevo or Resend keys from env files and rotate them if they were
ever shared.
