# Purge committed secrets from git history

> **خطر**: هذا يُعيد كتابة git history. كل من له fork أو clone لا بد أن يعيد
> clone بعد ذلك، أو يُجبَر force-pull. نسّق مع كل الفريق قبل التنفيذ.

## 1) قبل ما تبدأ

- تأكد إن الكل دفع شغله المحلي إلى remote (هذه آخر فرصة للحفاظ على
  commits لم تُرفع).
- خذ نسخة احتياطية كاملة:
  ```bash
  git clone --mirror git@host:Zadana/Zadana-Backend.git Zadana-Backend.backup.git
  ```

## 2) حضّر قائمة الأسرار للحذف

أنشئ ملف `secrets-to-purge.txt` يحتوي على الأسرار الحقيقية المُسرَّبة (راجع
git logs الموجودة قبل تطهيرها). مثال:

```
Password=4Pq!?g7LiD_3==>Password=__REDACTED__
SuperSecretKeyForZadanaMarketplace2026_!!_@@_VeryLongKeyToMeetRequirements==>__REDACTED__
re_Aqw1mqeR_EvyaPaFbGs7P3UZQ6qU4VsPm==>__REDACTED__
private_I+B7d2/bfoZkFllZCf07835bjb8===>__REDACTED__
sk_test_jeibBfSWQV1x7xuiCVZ1UB7ugqkYJ4BEud8dBA2z==>__REDACTED__
whsec_Zadana2026MoyasarWebhook_xK9mP4qR7vL2==>__REDACTED__
75865356e4573e2a6fafeed04bcd82c2808bbedcac0b1bad866af5c79e74ac51==>__REDACTED__
212bb8e555cd0f73e2d2fa048afa89469cfa5c14934e3a18d7b4d31f3e72d584==>__REDACTED__
ZXlKaGJHY2lPaUpJVXpVeE1pSXNJblI1Y0NJNklrcFhWQ0o5...==>__REDACTED__
DB23CFF33AC1EE0DD2CB2E0FE8F54E4F==>__REDACTED__
```

## 3) ثبّت `git-filter-repo`

```bash
# Linux / macOS
brew install git-filter-repo

# Windows (via pip)
pip install git-filter-repo
```

## 4) شغّل التطهير

```bash
cd Zadana-Backend
git filter-repo \
    --replace-text secrets-to-purge.txt \
    --paths-from-file <(printf 'temp_build/\n.tmp-build/\nartifacts/\n') \
    --invert-paths
```

> الـ `--invert-paths` يحذف هذه المسارات من history كاملاً.

## 5) اتحقّق

```bash
# يجب ألا تجد أي من الأسرار الحقيقية في history
git log -p --all -S "4Pq!?g7LiD_3" | head -20
git log -p --all -S "SuperSecretKeyForZadanaMarketplace2026" | head -20
```

## 6) ادفع النسخة الجديدة

```bash
git remote add origin git@host:Zadana/Zadana-Backend.git
git push --force-with-lease --all
git push --force-with-lease --tags
```

## 7) كل المطورين يعيدون clone

```bash
cd /tmp
mv Zadana-Backend Zadana-Backend.old   # احتياطي
git clone git@host:Zadana/Zadana-Backend.git
```

## 8) بعدها

- اعتبر كل سر مكتوب سابقاً مُخترقاً (compromised) حتى بعد التطهير، لأن النسخ
  المحلية قد لا تزال موجودة. **rotate كل شيء**.
- فعّل **secret scanning** على الـ repo (GitHub: Settings → Code security).
- أضف pre-commit hook يفحص `appsettings*.json` ضد قائمة patterns ممنوعة.
