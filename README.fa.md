# SvgLiveEditor

SvgLiveEditor یک برنامهٔ متن‌باز ویندوزی برای ویرایش کد SVG/XML و دیدن نتیجه در همان لحظه است. این برنامه برای کاربران و توسعه‌دهندگانی مناسب است که می‌خواهند Source را مستقیم ویرایش کنند و هم‌زمان چند ابزار دیداری ساده و امن هم داشته باشند. متن فارسی و انگلیسی با UTF-8 دقیق نگه‌داری می‌شود.

در نسخهٔ ۰٫۱۰٫۰، Stage 1 ویرایش دیداری، می‌توانید چند عنصر را انتخاب و با هم جابه‌جا کنید، Group/Ungroup انجام دهید، عناصر را هم‌تراز یا با فاصلهٔ یکسان بچینید و از Snap کمک بگیرید. Source، یعنی متن اصلی SVG، همیشه مرجع نهایی سند است.

مخزن پروژه: [github.com/Mammad3861/svg-live-editor](https://github.com/Mammad3861/svg-live-editor)

## امکانات اصلی

- ویرایشگر Source بر پایهٔ AvalonEdit با رنگ‌آمیزی XML، شمارهٔ خط، Undo/Redo، Find/Replace و Word Wrap غیرمخرب. منوی راست‌کلیک خود برنامه فرمان‌های معمول متن را دارد و انتخاب موجود را حفظ می‌کند.
- Live Preview، یعنی نمایش زندهٔ نتیجه، با به‌روزرسانی خودکار پس از حدود ۳۰۰ میلی‌ثانیه و دکمهٔ Refresh برای بازخوانی دستی.
- پشتیبانی دقیق از UTF-8، متن فارسی و فایل‌های SVG و TXT، بدون مرتب‌سازی یا بازنویسی خودکار کد.
- تب Layers برای دیدن عناصر گرافیکی و گروه‌ها، و تب Structure برای دیدن ساختار کامل XML.
- Properties برای ویرایش محدود و امن ویژگی‌های رایج شکل‌ها و متن.
- ساخت rect، circle، ellipse، line، text و گروه خالی از داخل برنامه.
- انتخاب چندگانهٔ محدود، حرکت گروهی، Nudge یا حرکت مرحله‌ای با کلید جهت، Duplicate، Delete، Group/Ungroup، تغییر ترتیب لایه‌ها، Align، Distribute و Snap.
- Zoom In، Zoom Out، Reset و Fit روی یک پس‌زمینهٔ شطرنجی ثابت که شفافیت را نشان می‌دهد. حالت Zoom انتخاب‌شده برای اجرای بعدی ذخیره می‌شود.
- Pan با Space و Drag، دکمهٔ وسط ماوس یا حالت اختصاصی Pan.
- کپی کل تصویر معتبر به‌صورت PNG و Drag کردن همان PNG به برنامه‌های ویندوزی.
- Drag & Drop یک فایل محلی SVG یا TXT روی تمام پنجره.
- پنج Template داخلی: Blank Canvas، App Icon، Social Card، Flow Diagram و Persian/RTL.
- Crash Recovery، باز کردن دوبارهٔ آخرین سند و Auto Save اختیاری.
- هشدار تغییرات ذخیره‌نشده پیش از بستن یا جایگزین کردن سند؛ Cancel سند فعلی را دست‌نخورده نگه می‌دارد.
- خطای واضح XML همراه با شمارهٔ خط و ستون؛ XML نامعتبر آخرین Preview معتبر را نگه می‌دارد.
- یک سند Welcome امن و اصلی با متن فارسی و انگلیسی برای شروع سریع.
- آیکون چنداندازه‌ای اختصاصی در فایل اجرایی، نوار عنوان، Taskbar و Alt+Tab.

قالب‌ها با همان اعتبارسنجی سخت‌گیرانهٔ فایل‌های عادی بررسی می‌شوند. هر Template به‌صورت یک سند جدا و Modified باز می‌شود و به Save As نیاز دارد؛ فایل داخلی برنامه هیچ‌وقت بازنویسی نمی‌شود. قالب فارسی برای جهت درست متن از direction با مقدار rtl، unicode-bidi با مقدار embed و text-anchor با مقدار start استفاده می‌کند. جای‌نگهدارهای خنثی مقدار plaintext دارند و برنامه نویسهٔ جهت نامرئی به متن اضافه نمی‌کند.

## دانلود و پیش‌نیازها

فایل ZIP نسخهٔ ویندوز را از [GitHub Releases](https://github.com/Mammad3861/svg-live-editor/releases) بگیرید. نام فایل به این شکل است:

```text
SvgLiveEditor-vX.Y.Z-win-x64.zip
```

برای اجرای برنامه به این موارد نیاز دارید:

- Windows 10 یا Windows 11 روی معماری x64؛
- [Microsoft Edge WebView2 Evergreen Runtime](https://developer.microsoft.com/microsoft-edge/webview2/#download-section).

بستهٔ ZIP به‌صورت self-contained ساخته می‌شود و .NET Desktop Runtime را داخل خود دارد. بنابراین برای اجرای نسخهٔ بسته‌بندی‌شده نیازی به نصب جداگانهٔ .NET ندارید. WebView2 Evergreen Runtime داخل ZIP نیست و باید جداگانه روی ویندوز نصب باشد.

GitHub مقدار Digest، یعنی اثر انگشت فایل، را زیر ZIP نشان می‌دهد. برای محاسبهٔ SHA-256 فایل دانلودشده و مقایسه با آن Digest، در PowerShell اجرا کنید:

```powershell
Get-FileHash .\SvgLiveEditor-v0.10.0-win-x64.zip -Algorithm SHA256
```

## شروع سریع

1. تمام محتوای ZIP را در یک پوشهٔ تازه Extract کنید. برنامه را مستقیم از داخل ZIP اجرا نکنید.
2. فایل **SvgLiveEditor.exe** را اجرا کنید.
3. از **File > Open** یک فایل SVG یا TXT باز کنید، یا از **File > New from Template** یک Template بسازید.
4. کد را در Source ویرایش کنید و نتیجه را در Live Preview ببینید.
5. برای تغییرات دیداری، ابزار Select را فعال و عنصر را از Preview، Layers یا Structure انتخاب کنید.
6. سند را با Save یا Save As ذخیره کنید. ذخیره با UTF-8 بدون BOM انجام می‌شود و قالب کد شما تغییر نمی‌کند.

اگر Source نامعتبر شود، آخرین Preview معتبر روی صفحه می‌ماند، اما ابزارهای ویرایش دیداری تا معتبر شدن دوبارهٔ سند غیرفعال می‌شوند.

Open و Drag & Drop فقط یک فایل محلی SVG یا TXT را با UTF-8 سخت‌گیرانه و حداکثر اندازهٔ ۱۰٬۰۰۰٬۰۰۰ بایت می‌پذیرند. پوشش Drop روی Source، Inspector، Properties و Live Preview کار می‌کند. پیش از جایگزینی سند، همان هشدار تغییرات ذخیره‌نشده نمایش داده می‌شود.

## ویرایش دیداری و Layers

### Layers، Structure و انتخاب

Layers فقط عناصر گرافیکی و گروه‌های تودرتو را نشان می‌دهد و عنصر جلویی را بالاتر می‌گذارد. بخش‌هایی مثل تعریف Gradient، Marker، Mask، Filter و Metadata در Layers نمایش داده نمی‌شوند. Structure در مقابل، سلسله‌مراتب کامل XML را نگه می‌دارد. گروه‌ها فلش باز و بسته شدن استاندارد و قابل استفاده با صفحه‌کلید دارند و وضعیت باز بودنشان حفظ می‌شود.

کلیک معمولی یک مورد را انتخاب می‌کند. Ctrl+Click یک مورد را به انتخاب اضافه یا از آن حذف می‌کند. Shift+Click در درخت، یک بازهٔ پیوسته از فرزندان قابل‌مشاهدهٔ یک والد را انتخاب می‌کند؛ در Preview نیز Shift+Click انتخاب یک عنصر را روشن یا خاموش می‌کند. برنامه حداکثر ۱۲۸ مورد را نگه می‌دارد. یکی از آن‌ها Primary، یعنی انتخاب اصلی، است و Properties همان مورد را دنبال می‌کند. کلیک ماوس در Layers انتخاب فعلی متن Source را بی‌دلیل عوض نمی‌کند؛ فرمان آشکارسازی با صفحه‌کلید می‌تواند Start Tag مورد اصلی را در Source نشان دهد.

انتخاب‌ها به نسخهٔ فعلی Source وابسته‌اند. اگر یکی از اعضا قدیمی، قفل، مبهم یا پشتیبانی‌نشده باشد، کل عملیات رد می‌شود. برنامه هیچ زیرمجموعه‌ای از انتخاب را جداگانه جابه‌جا یا ویرایش نمی‌کند.

### ساخت، نام‌گذاری، Duplicate و Delete

کنترل Add و منوی راست‌کلیک Layers/Structure فقط شش نوع مشخص را می‌سازند: rect، circle، ellipse، line، text و گروه خالی. اندازه و جای اولیه از Canvas یا viewBox معتبر گرفته می‌شود. مقصد نیز باید صریحاً ریشهٔ SVG یا گروه و موقعیت انتخاب‌شده باشد؛ برنامه مقصد را حدس نمی‌زند.

عنصر جدید بلافاصله در هر دو درخت و Properties انتخاب می‌شود و فقط یک مرحله Undo می‌سازد. Rename Layer تنها ویژگی data-name را تغییر می‌دهد. نام فارسی، انگلیسی و ترکیبی پذیرفته می‌شود، اما id فنی SVG و ارجاع‌های داخلی دست‌نخورده می‌مانند. پاک کردن نام، data-name را حذف می‌کند.

Duplicate برای شناسه‌ها مقدار یکتای قطعی می‌سازد و ارجاع‌های محلی معتبر داخل زیرشاخهٔ کپی‌شده را هم اصلاح می‌کند. اگر هدف یک ارجاع گم‌شده یا مبهم باشد، عملیات رد می‌شود. Delete و تغییر والد، یا Reparent، نیز Source فعلی، قفل‌ها، والدها، محدودهٔ definitions و اثر ویژگی‌های موروثی را دوباره بررسی می‌کنند.

میان‌برهای Duplicate و Delete فقط وقتی اجرا می‌شوند که Preview، Layers، Structure یا بخش غیرقابل‌ویرایش Properties فوکوس داشته باشد. این میان‌برها ویرایش متن در Source، فیلدهای Properties، Font picker، Rename درجا یا نوشتن با IME را تصاحب نمی‌کنند.

### Move، Resize، Group و چیدمان

rect، circle، ellipse، line و text ساده‌ای که با روش امن اندازه‌گیری شده باشد، می‌توانند به‌صورت یک انتخاب واحد یا چندگانه جابه‌جا شوند. Drag یا کلیدهای جهت، همهٔ انتخاب را با هم حرکت می‌دهد. Shift همراه کلید جهت، حرکت را از ۱ به ۱۰ واحد SVG افزایش می‌دهد. Preview هنگام Drag فقط Outline پیشنهادی را حرکت می‌دهد؛ Source یک‌بار و هنگام پایان عملیات تغییر می‌کند.

Resize فعلاً فقط برای یک عنصر فعال است. مستطیل و بیضی هشت Handle، دایره چهار Handle و خط دو Handle انتهایی دارند. کمترین اندازهٔ شکل ۰٫۰۱ واحد SVG است و عبور از لبهٔ ثابت، شکل را وارونه نمی‌کند.

Group فقط ۲ تا ۱۲۸ عنصر پیوسته و هم‌والد را می‌پذیرد. این فرمان یک گروه واقعی در Source می‌سازد، عناصر را داخل آن قرار می‌دهد و گروه تازه را انتخاب اصلی می‌کند. Ungroup فقط یک گروه خنثی، غیرخالی، بدون Attribute و بدون Animation وابسته به خود گروه را باز می‌کند. اگر عملیات امن نباشد، دلیل دقیق در Status Bar دیده می‌شود.

Arrange شامل جلو و عقب بردن عناصر هم‌والد، شش فرمان Align و توزیع افقی یا عمودی با فاصلهٔ برابر است. همهٔ اعضا باید قابل‌اندازه‌گیری، قابل‌حرکت، باز و در یک سیستم مختصات هم‌والد باشند. هر فرمان موفق فقط یک Undo می‌سازد. Drag در Layers برای جای‌گذاری before، after یا inside و تغییر والد است و با Group فرق دارد.

گزینهٔ **View > Snap to objects** به‌طور پیش‌فرض روشن است و برای همان کاربر ذخیره می‌شود. Snap یعنی چسباندن کنترل‌شدهٔ لبه یا مرکز عنصر در حال حرکت به لبه یا مرکز یک عنصر مرتبط یا مرکز Canvas. فقط عنصرهای هم‌والد، قابل‌مشاهده، باز و قابل‌حرکت نامزد می‌شوند و خود انتخاب کنار گذاشته می‌شود. آستانه ۴ پیکسل CSS است و با Fit یا Zoom به واحد SVG تبدیل می‌شود. نزدیک‌ترین اصلاح معتبر انتخاب می‌شود؛ حالت‌های مبهم و پس‌زمینه‌های تمام‌اندازه نادیده گرفته می‌شوند. در هر محور حداکثر یک Guide و فقط برای اصلاحی که واقعاً اعمال شده نشان داده می‌شود. Guide وارد Source یا PNG نمی‌شود.

با خاموش کردن Snap، هیچ اصلاح یا Guide تازه‌ای ساخته نمی‌شود. Guide موقت با پایان یا لغو Drag، تغییر انتخاب یا Mode، قدیمی شدن نسخهٔ Source و نامعتبر شدن سند پاک می‌شود.

### Properties، Opacity، نمایش و قفل

Properties فقط ویژگی‌های رایج و امن شکل و متن را ویرایش می‌کند. تغییر ثبت‌شده یک ویرایش کوچک در Source است و کل XML دوباره Serialize نمی‌شود. تغییر ثبت‌شده با Ctrl+Z و Ctrl+Y سند قابل بازگشت است؛ متن هنوز ثبت‌نشده در فیلد، Undo/Redo محلی همان فیلد را حفظ می‌کند. مقدار d برای Path فقط خواندنی است.

Font picker فقط نام اصلی و خوانای خانوادهٔ فونت را نشان می‌دهد، اما Stack معتبر فونت‌های جایگزین در Source و Tooltip حفظ می‌شود. انتخاب یا تایپ فونت جدید فقط خانوادهٔ اول را عوض می‌کند. فونت دانلود، Embed یا همراه برنامه بسته‌بندی نمی‌شود. برای text ساده، ویژگی‌های x، y، font-family، font-size، font-weight، font-style، fill، direction، unicode-bidi و text-anchor در دسترس‌اند. گزینه‌های خطرناک BiDi Override ارائه نمی‌شوند.

Opacity از ۰ تا ۱۰۰ درصد تنظیم می‌شود. حرکت Slider تا لحظهٔ رها کردن فقط مقدار پیشنهادی رابط را عوض می‌کند و بعد یک ویرایش Source ثبت می‌شود. مقدار تایپی با Enter یا خروج فوکوس ثبت و با Escape لغو می‌شود. مقدار ۱۰۰ درصد ویژگی اختیاری opacity را حذف می‌کند و fill-opacity یا stroke-opacity را تغییر نمی‌دهد. اگر Opacity از Style، Animation یا مقدار مبهم بیاید، یا عنصر Transform و Effect پشتیبانی‌نشده داشته باشد، کنترل غیرفعال می‌شود.

کنترل نمایش در Layers فقط در حالت بدون ابهام، ویژگی display با مقدار none را اضافه می‌کند. این تغییر داخل SVG است و بعد از Save و باز کردن دوباره باقی می‌ماند. برنامه آن را تنها زمانی حذف می‌کند که بداند این تغییر در نشست جاری ساخته شده است. display، visibility، Style یا Animation نوشته‌شدهٔ کاربر بازنویسی نمی‌شود.

قفل Layers فقط برای همان نشست است و داخل SVG ذخیره نمی‌شود. قفل مستقیم یا قفل والد جلوی Create، Duplicate، Delete، Group/Ungroup، Move، Resize، Nudge، Layout، Arrange، Reorder/Reparent، Opacity و Properties را می‌گیرد. Source عمداً همچنان قابل ویرایش است.

### پشتیبانی فعلی عناصر

| نوع محتوا | رفتار در Preview و Layers |
| --- | --- |
| rect، circle، ellipse و line | در صورت معتبر بودن هندسه، واحدها، والدها، Transform و Effect قابل انتخاب، Move و Resize هستند. |
| text ساده و مستقیم | پس از اندازه‌گیری محدود و معتبر قابل انتخاب و Move است. اندازهٔ متن از font-size در Properties تغییر می‌کند. |
| path، polygon و polyline | Move نمی‌شوند. اگر Bounding Box محافظه‌کارانه و مطمئن موجود باشد فقط برای بررسی انتخاب می‌شوند. عنصر پشتیبانی‌نشدهٔ جلویی اجازه نمی‌دهد عنصر زیر آن اشتباهی انتخاب شود. |
| گروه | قابل باز و بسته شدن، Reorder و Reparent است. Group/Ungroup فقط با قواعد محافظه‌کارانهٔ بالا انجام می‌شود. DOM گروه در Preview در دسترس JavaScript قرار نمی‌گیرد. |
| tspan، textPath و متن تودرتو یا پیچیده | ویرایش دیداری ندارد، اما از Source و Inspector قابل ویرایش است. |
| محتوای Transform، Clip، Mask، Filter، Animation، Hidden، Malformed یا واحد پشتیبانی‌نشده | Move دیداری ندارد و برای انتخاب عنصر زیرین از آن عبور نمی‌شود. |
| defs، Gradient، Marker، Pattern و محتوای تعریفی | به‌عنوان Artwork مستقل در Preview انتخاب نمی‌شود. |

## میان‌برهای صفحه‌کلید و ماوس

رفتار Ctrl+C به فوکوس واقعی صفحه‌کلید بستگی دارد، نه محل نشانگر ماوس. در Source یا فیلد Properties فقط متن انتخاب‌شده کپی می‌شود. وقتی Preview فوکوس دارد، همان میان‌بر PNG کامل Preview را کپی می‌کند.

| کار | میان‌بر یا حرکت |
| --- | --- |
| ساخت سند از Template | <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>N</kbd> |
| Undo / Redo | <kbd>Ctrl</kbd> + <kbd>Z</kbd> / <kbd>Ctrl</kbd> + <kbd>Y</kbd> |
| Word Wrap | <kbd>Alt</kbd> + <kbd>Z</kbd> یا <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>W</kbd> |
| Select tool | <kbd>V</kbd> |
| Pan mode | <kbd>H</kbd>؛ خروج با <kbd>Esc</kbd> |
| Rename Layer | <kbd>F2</kbd> |
| Duplicate | <kbd>Ctrl</kbd> + <kbd>D</kbd> |
| Delete انتخاب دیداری | <kbd>Delete</kbd> یا <kbd>Backspace</kbd> |
| Group / Ungroup | <kbd>Ctrl</kbd> + <kbd>G</kbd> / <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>G</kbd> |
| حرکت انتخاب | کلیدهای جهت؛ با <kbd>Shift</kbd> حرکت ۱۰ واحدی |
| تغییر انتخاب چندگانه | <kbd>Ctrl</kbd> + Click در درخت یا <kbd>Shift</kbd> + Click طبق قواعد هر سطح |
| Zoom دور نشانگر | <kbd>Ctrl</kbd> + Wheel |
| Scroll عمودی / افقی | Wheel / <kbd>Shift</kbd> + Wheel |
| Pan موقت | <kbd>Space</kbd> + Left Drag یا Middle Drag |
| جابه‌جایی Artwork | Left Drag روی عنصر قابل‌حرکت انتخاب‌شده |
| Drag کردن PNG | <kbd>Ctrl</kbd> + Left Drag روی Artwork؛ <kbd>Alt</kbd> فعلاً Alias سازگار است |
| کپی Preview به‌صورت PNG | <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>C</kbd> یا <kbd>Ctrl</kbd> + <kbd>C</kbd> با فوکوس Preview |
| کپی تمام Source | <kbd>Ctrl</kbd> + <kbd>Alt</kbd> + <kbd>C</kbd> |

Pan mode و Space+Drag همیشه بر Move، انتخاب و Drag تصویر اولویت دارند. Modifierهای ترکیبی و مبهم پذیرفته نمی‌شوند. Plain Left Drag برای Move است؛ Ctrl+Drag از همان مسیر امن PNG استفاده می‌کند و Source را تغییر نمی‌دهد.

شروع Drag از فضای شطرنجی، ترکیب Modifierها یا حرکت کمتر از آستانهٔ استاندارد Drag ویندوز، PNG خروجی نمی‌سازد. مسیر مستقیم و کنترل **Drag Image** هر دو از همان Render اعتبارسنجی‌شده استفاده می‌کنند و یک فایل واقعی PNG با شفافیت می‌دهند.

منوی راست‌کلیک Preview فقط فرمان‌های ثابت **Copy Preview as PNG**، **Fit** و **Reset Zoom** را نشان می‌دهد. منوی مرورگر، Inspect، Print، Save Page و Navigation در دسترس نیستند. کنترل **Copy Image** و **Drag Image** هم مسیرهای قابل‌فوکوس و قابل‌استفاده با صفحه‌کلید هستند.

فرمان **Edit > Copy Entire SVG Source** متن کامل و دقیق ویرایشگر را کپی می‌کند. پایان خط‌ها، متن فارسی، XML نامعتبر، Caret، انتخاب و وضعیت Modified تغییر نمی‌کنند.

## محدودیت‌های فعلی

- Source همیشه مرجع اصلی است. ابزارهای دیداری فقط ویرایش‌های محدود و اعتبارسنجی‌شده را روی همان متن اعمال می‌کنند.
- Artwork که کاملاً بیرون Canvas باشد، مستقیماً از Preview قابل انتخاب نیست. برای دسترسی به آن از Layers، Structure یا Source استفاده کنید.
- ویرایشگر Source عمداً چپ‌به‌راست است تا Tagها و نشانه‌های XML قابل پیش‌بینی بمانند. در متن ترکیبی فارسی/انگلیسی، Highlight انتخاب ممکن است در AvalonEdit تکه‌تکه به نظر برسد، اما انتخاب منطقی، Preview و متن ذخیره‌شده دقیق باقی می‌مانند.
- Resize هم‌زمان چند انتخاب، Rotation، Resize یا Move مسیر Path، Ruler، Guide دستی، و تغییر لحظه‌ای خود Artwork در هر حرکت Pointer به نسخه‌های بعد موکول شده‌اند. هنگام Drag فقط Overlay پیشنهادی جابه‌جا می‌شود.
- Resize متن با Handle، ویرایش مستقیم متن روی Canvas، Text layout پیچیده، Path node editing و پیشنهادهای پیشرفتهٔ Smart Layout در این نسخه نیستند.
- مسیر دقیق before/after/inside برای Reparent از Drag در Layers استفاده می‌کند. **Move to SVG Root (Front)** مسیر صفحه‌کلیدی خروج از گروه است؛ انتخاب مقصد گروه با صفحه‌کلید هنوز یک بهبود آینده است.
- برنامه فقط یک زیرمجموعهٔ محدود و امن از SVG را Preview می‌کند. باز شدن همهٔ فایل‌های خروجی Illustrator یا Inkscape تضمین نمی‌شود.
- تصویر، فونت و Style خارجی، Data Resource، Link، عنصر style، foreignObject، Script و Event Handler رد می‌شوند.
- Copy و Drag کردن PNG پشتیبانی می‌شود، اما Export مستقل فایل PNG/PDF، Installer و Automatic Update هنوز وجود ندارد.
- WebView2 Evergreen Runtime همچنان یک پیش‌نیاز خارجی است.
- Auto Save فقط برای SVG معتبر و فایل محلی واجد شرایط فعال می‌شود و جای Version Control یا Backup را نمی‌گیرد.

این محدودیت‌ها بخشی از مرز آگاهانهٔ نسخهٔ ۰٫۱۰٫۰ هستند و برای دور زدن آن‌ها، اعتبارسنجی یا مدل امنیتی ضعیف نشده است.

## حریم خصوصی و امنیت

SvgLiveEditor فایل بازشده را غیرقابل اعتماد فرض می‌کند. پردازش در خود برنامه انجام می‌شود؛ Telemetry، سرویس Cloud و دانلود فونت وجود ندارد. Preview یک مرورگر عمومی نیست، دسترسی فایل SVG به شبکه یا WebView2 را باز نمی‌کند و جای بررسی فایل پیش از انتشار عمومی را نمی‌گیرد.

### اعتبارسنجی و Preview

- DTD، تعریف Entity و دسترسی External Entity ممنوع است.
- ریشه باید SVG با Namespace استاندارد باشد.
- Script، Event Handler درون‌خطی، foreignObject، محتوای فعال، Processing Instruction، عنصر style، Link و Resource غیرفرگمنت، یعنی آدرسی غیر از ارجاع داخلی با #، رد می‌شوند.
- SVG معتبر ابتدا به UTF-8 و کدگذاری متنی Base64 تبدیل می‌شود و فقط به‌صورت Data Image نمایش داده می‌شود. کد خام SVG هرگز داخل HTML میزبان قرار نمی‌گیرد.
- سیاست امنیت محتوا یا CSP فقط Script ثابت خود برنامه را با Hash دقیق SHA-256 مجاز می‌کند. unsafe-inline، unsafe-eval و Host Object فعال نیستند.
- انتخاب و Hit Test در میزبان .NET انجام می‌شود. پیام‌های WebView2 به Token ناوبری، نسخهٔ Source، Schema، مختصات و محدوده‌های مشخص وابسته‌اند؛ پیام دلخواه پذیرفته نمی‌شود.
- Overlay فقط هندسهٔ محدود ساخته‌شده در میزبان، یک انتخاب Primary و حداکثر یک Snap Guide در هر محور را دریافت می‌کند.
- Zoom خود WebView2 روی ۱۰۰ درصد ثابت می‌ماند. فقط اندازهٔ تصویر SVG تغییر می‌کند.
- Permission، Download، Popup، Navigation و Resource خارجی، Developer Tools و منوی پیش‌فرض مرورگر مسدود هستند.
- پاسخ‌های دیررس نمی‌توانند Preview جدیدتر را جایگزین کنند. فقط آخرین نسخهٔ معتبر پذیرفته می‌شود.

Layers و Structure فقط از Source پذیرفته‌شده توسط همین Validator ساخته می‌شوند. آن‌ها نمای بومی WPF از محدوده‌های دقیق Source هستند و DOM فایل SVG را به JavaScript یا WebView2 نمی‌دهند. تغییر نام، Properties، Opacity، Visibility، Arrange، Reorder، Reparent و ویرایش دیداری، قبل از یک تغییر کوچک در Source دوباره نسخه، محدوده، قفل و ساختار فعلی را بررسی می‌کنند. عملیات مبهم Fail Closed است؛ یعنی به‌جای حدس زدن، کاملاً رد می‌شود.

### PNG و Drag Image

PNG از کل تصویر معتبر و شفاف ساخته می‌شود، نه فقط بخش دیده‌شدهٔ Viewport. Outline، Handle و Snap Guide وارد PNG نمی‌شوند. Copy یا Drag تصویر، Zoom و وضعیت سند را تغییر نمی‌دهد. اندازه از width و height معتبر یا نسبت و ابعاد viewBox گرفته می‌شود و با حفظ نسبت، به حداکثر ۴۰۹۶ پیکسل در هر ضلع و ۸٬۰۰۰٬۰۰۰ پیکسل در مجموع محدود است؛ این مقدار تقریباً ۳۲ مگابایت RGBA فشرده‌نشده است.

اگر Source نامعتبر باشد، Copy یا Drag عمداً از آخرین Preview معتبر استفاده می‌کند و این موضوع را در Status Bar می‌گوید. اگر هنوز اعتبارسنجی در حال انجام باشد، خروجی به‌عنوان آخرین Preview اعتبارسنجی‌شده معرفی می‌شود. بدون Preview معتبر و قابل‌مشاهده، فرمان غیرفعال است یا یک خطای غیرمخرب نشان می‌دهد.

برای Drag & Drop بین برنامه‌های ویندوز، PNG با یک نام تصادفی در پوشهٔ اختصاصی کاربر نوشته می‌شود:

```text
%LocalAppData%\SvgLiveEditor\DragOut
```

Source کنار آن ذخیره نمی‌شود. Drag لغوشده در صورت امن بودن پاک می‌شود. فایل Drag موفق برای برنامه‌هایی که آن را با تأخیر می‌خوانند موقتاً می‌ماند. پاک‌سازی هنگام شروع، قبل از Drag بعدی و هر شش ساعت انجام می‌شود. فایل‌های قدیمی‌تر از ۲۴ ساعت حذف می‌شوند و پوشه حداکثر ۲۰ فایل و ۲۰۰٬۰۰۰٬۰۰۰ بایت نگه می‌دارد. شکست پاک‌سازی مانع اجرای برنامه نمی‌شود.

### Recovery و Auto Save

Crash Recovery به‌طور پیش‌فرض روشن است و متن دقیق UTF-8، حتی XML نامعتبر و فارسی، را پس از یک تأخیر کوتاه در این پوشه نگه می‌دارد:

```text
%LocalAppData%\SvgLiveEditor\Recovery
```

Snapshot با شناسهٔ تصادفی و جایگزینی Atomic، یعنی یک‌مرحله‌ای، ساخته می‌شود. هنگام شروع برنامه، گزینه‌های Restore، Discard و Skip پیش از باز شدن آخرین سند نمایش داده می‌شوند. Restore فقط Snapshot را در حافظه و با وضعیت Modified باز می‌کند و فایل اصلی را بازنویسی نمی‌کند. Snapshot یکسان با فایل اصلی بدون پیام حذف می‌شود. نگه‌داری به ۷ روز، ۱۰ فایل و ۱۰۰٬۰۰۰٬۰۰۰ بایت محدود است.

Loader فقط Schema نسخه‌دار دقیق، نام و شناسهٔ هماهنگ، Metadata معتبر، مسیر محلی پشتیبانی‌شده، اندازهٔ مجاز و SHA-256 درست را می‌پذیرد. Reparse Point ویندوز، مانند پیوندهای فایل‌سیستمی، و رکورد خراب یا دست‌کاری‌شده رد می‌شوند. مسیر ذخیره‌شده هیچ‌وقت برای خواندن متن Snapshot دنبال نمی‌شود.

Auto Save به‌طور پیش‌فرض خاموش است. اگر روشن شود، فقط یک فایل موجود، محلی، نام‌دار و قابل‌نوشتن SVG یا TXT را که قبلاً دستی Open یا Save شده باشد، دو ثانیه پس از آخرین ویرایش ذخیره می‌کند. Source باید در همان نسخه توسط Validator پذیرفته شود. دادهٔ دقیق UTF-8 کنار فایل آماده و سپس به‌صورت Atomic جایگزین می‌شود.

XML نامعتبر، فایل گم‌شده یا Read-only، مسیر شبکه، Reparse Point، Drive پشتیبانی‌نشده یا تغییر نشست سند، Auto Save را بدون دست زدن به فایل اصلی متوقف می‌کند. فایل اصلی گم‌شده دوباره ساخته نمی‌شود و Recovery ادامه پیدا می‌کند. سند Untitled و Template همچنان Save As می‌خواهد.

آخرین سند نام‌داری که با موفقیت Open یا Save شده باشد، به‌طور پیش‌فرض در اجرای بعد باز می‌شود. این ترجیح و مسیر کامل فقط در تنظیمات LocalAppData همان کاربر ذخیره می‌شود. مسیر گم‌شده، غیرقابل‌دسترسی یا پشتیبانی‌نشده بدون خطا به سند Welcome برمی‌گردد.

## توسعه، ساخت و انتشار

### پیش‌نیاز توسعه

- Windows 10 یا Windows 11؛
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)، نسخهٔ 10.0.302 یا Feature Band جدیدتر و سازگار با .NET 10.

نسخه‌های ثابت NuGet عبارت‌اند از AvalonEdit 6.3.1.120، Microsoft.Web.WebView2 1.0.4078.44، MSTest 4.3.2 و Microsoft.NET.Test.Sdk 18.8.1.

### Restore، Build، Test و Run

```powershell
dotnet restore SvgLiveEditor.sln
dotnet build SvgLiveEditor.sln --configuration Release --no-restore
dotnet test SvgLiveEditor.sln --configuration Release --no-build
dotnet run --project src/SvgLiveEditor/SvgLiveEditor.csproj
```

### ساخت بستهٔ win-x64

```powershell
dotnet publish src/SvgLiveEditor/SvgLiveEditor.csproj --configuration Release --runtime win-x64 --self-contained true --property:PublishProfile=win-x64
```

خروجی در این مسیر نوشته می‌شود:

```text
dist/win-x64
```

برای Publish محلی، Audit بسته و ساخت ZIP و فایل SHA-256 نسخه‌دار اجرا کنید:

```powershell
./scripts/Publish-WinX64.ps1 -Version 0.10.0
```

فایل‌های زیر ساخته می‌شوند:

```text
releases/SvgLiveEditor-v0.10.0-win-x64.zip
releases/SvgLiveEditor-v0.10.0-win-x64.sha256
```

دستور محلی هیچ GitHub Release را ایجاد یا تغییر نمی‌دهد. بسته عمداً Trim، ReadyToRun یا Single-file نشده است. ساختار پوشه‌ای برای سازگاری WPF، وابستگی‌های Native در WebView2، پایداری شروع برنامه و Baseline اندازهٔ بسته حفظ شده است.

### انتشار خودکار GitHub

[Release workflow](.github/workflows/release.yml) با Push شدن یک Tag پایدار با الگوی vX.Y.Z اجرا می‌شود. اجرای دستی برای یک Tag موجود هم ممکن است. Workflow همان Commit دقیق را Checkout می‌کند، Tag و نسخهٔ پروژه را بررسی می‌کند، Restore و Release Build را انجام می‌دهد، Testها را به‌جز DesktopIntegration اجرا می‌کند و همان Script محلی را برای ساخت و Audit بسته به کار می‌برد.

ZIP و فایل داخلی SHA-256 به‌عنوان Artifact کوتاه‌مدت GitHub Actions نگه‌داری می‌شوند، اما فقط ZIP به Release جدید پیوست می‌شود. Checksum پیش از Upload بررسی و در Log و Job Summary ثبت می‌شود.

اگر Release موجود باشد، وضعیت انتشار، متن ویرایش‌شدهٔ دستی و فایل‌های تاریخی نامرتبط آن حفظ می‌شوند و فقط ZIP هم‌نام جایگزین می‌شود. اگر Release وجود نداشته باشد، Workflow یک Draft با Release Notes خودکار GitHub می‌سازد، در صورت وجود Tag پایدار قبلی را مبنا قرار می‌دهد و Draft را برای بازبینی دستی منتشرنشده نگه می‌دارد.

### آیکون و ساختار مخزن

منبع قابل‌ویرایش آیکون و فایل ICO ویندوز در این مسیرها هستند:

```text
assets/app-icon.svg
src/SvgLiveEditor/Assets/SvgLiveEditor.ico
```

فایل ICO اندازه‌های ۱۶، ۲۴، ۳۲، ۴۸، ۶۴، ۱۲۸ و ۲۵۶ پیکسل را دارد و داخل فایل اجرایی Compile می‌شود. برای تولید دوبارهٔ قطعی آن در ویندوز:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./scripts/Generate-AppIcon.ps1
```

این Script توسعه فقط از System.Drawing داخلی ویندوز استفاده می‌کند و وابستگی Runtime تازه‌ای به برنامه اضافه نمی‌کند.

ساختار اصلی مخزن:

```text
src/SvgLiveEditor/          برنامهٔ WPF، Inspector و سرویس‌های ویرایش امن
tests/SvgLiveEditor.Tests/ Testهای منطق و امنیت
samples/welcome.svg        سند Welcome امن و اصلی
templates/                 پنج Template امن و داخلی
assets/                    فایل‌های گرافیکی اصلی و قابل‌ویرایش
docs/                      مستندات معماری، امنیت و Roadmap
.github/workflows/         Workflowهای Build، Test و Release ویندوز
scripts/                   ابزارهای Publish محلی
```

مسیر تعامل امن با Testهای Browser Integration پوشش داده شده است؛ از جمله اندازه‌گیری متن فارسی و انگلیسی با Chromium، Resize واقعی با Pointer و درخواست‌های Context Menu وابسته به نسخه و انتخاب.

با این حال، Multi-selection در Preview/Layers، Multi-drag/Nudge، Group/Ungroup، همهٔ فرمان‌های Align/Distribute، Snap در Fit و Zoom دستی، Add/Duplicate/Delete، Reparent، Lock، Resize، Opacity، Font، Drag & Drop، Ctrl+Wheel، Ctrl+C وابسته به فوکوس، Touchpad و Pan باید برای هر Release روی دستگاه مقصد هم بررسی فیزیکی شوند.

## Roadmap، مشارکت و مجوز

[Roadmap فارسی](docs/roadmap.fa.md) قابلیت‌های منتشرشده، کارهای ضروری پیش از نسخهٔ ۱ و ایده‌های اختیاری بعد از آن را جدا می‌کند.

پیش از ارسال تغییر، [CONTRIBUTING.md](CONTRIBUTING.md) را بخوانید. فقط نمونه و Assetی را اضافه کنید که خودتان ساخته‌اید یا اجازهٔ روشن برای بازنشر آن دارید. SVG، متن، Layout، مختصات یا Archive کپی‌شده با مجوز نامشخص پذیرفته نمی‌شود.

برای گزارش Bug، نسخه و معماری ویندوز نمایش‌داده‌شده در **Help > About SvgLiveEditor** را همراه با مراحل بازتولید بنویسید.

مشکل امنیتی قابل سوءاستفاده را پیش از بررسی در Issue عمومی منتشر نکنید. روش گزارش خصوصی در [بخش Security Issues راهنمای مشارکت](CONTRIBUTING.md#security-issues) آمده است. توضیح کامل مدل امنیتی نیز در [docs/security-model.md](docs/security-model.md) قرار دارد.

کد این مخزن با [مجوز MIT](LICENSE) منتشر می‌شود.
