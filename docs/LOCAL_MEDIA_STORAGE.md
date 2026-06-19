# Local media storage

The API can store uploaded media in a persistent folder outside the publish
directory while IIS serves that folder from a separate static site.

## API configuration

Set these environment variables on the API application:

```text
FileStorage__Provider=Local
FileStorage__Local__RootPath=D:\ZadanaMedia
FileStorage__Local__PublicBaseUrl=https://media.zadna0.com
FileStorage__Local__ConvertImagesToWebp=true
FileStorage__Local__WebpQuality=82
FileStorage__Local__MaxWidth=2000
FileStorage__Local__MaxHeight=2000
FileStorage__Local__MaxPixelCount=40000000
FileStorage__Local__MaxConcurrentImageProcessors=2
```

The API process identity needs Modify permission on `D:\ZadanaMedia`. Do not
place this directory inside the API publish directory because deployment may
delete it.

JPEG, PNG, WebP, GIF, and BMP uploads are decoded, resized when necessary, and
saved with a unique `.webp` filename. Animated images are stored as a static
WebP frame. PDFs remain PDF. The original raster image is not retained.

Only two image conversions run concurrently by default, preventing upload
bursts from consuming all API CPU.

## DNS and IIS

1. Add an `A` record: `media.zadna0.com` -> the API server IP.
2. Create a separate IIS site with physical path `D:\ZadanaMedia`.
3. Add HTTPS binding for `media.zadna0.com` and install its certificate.
4. Copy `deploy/media-web.config` to `D:\ZadanaMedia\web.config`.
5. Give the media IIS app-pool identity Read permission.
6. Keep directory browsing disabled.

Files use GUID names, so IIS can safely cache them for one year. Updating an
image creates a new URL; clients will not remain stuck on an obsolete image.

## Backup

Back up the whole media root daily. Database backups alone are not enough
because the database stores media URLs, not the binary files.
