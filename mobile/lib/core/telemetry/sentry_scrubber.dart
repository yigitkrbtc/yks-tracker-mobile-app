import 'package:sentry_flutter/sentry_flutter.dart';

/// ADR-0022 §5 katman 2: izin listesi süzgeci. Gönderilen olay, izinli alanlar kopyalanarak yeniden
/// kurulur; SDK'nın ileride eklediği ya da JSON'dan gelen (`unknown`) alanlar böylece kendiliğinden
/// düşer. Asıl koruma kayıtlara kişisel veri yazmamaktır; maskeleme derinlemesine savunmadır.

/// Kırıntılardan yalnız bizim yazdıklarımız (bu önekle) gönderilir.
const breadcrumbCategoryPrefix = 'nirengi.';

const _redacted = '[redacted]';
const _allowedTags = {'route', 'method', 'status'};

final _piiPatterns = [
  // E-posta (Türkçe harfler ve URL kodlu '@' dahil).
  RegExp(
    r'[\p{L}\p{N}._%+\-]+(?:@|%40)[\p{L}\p{N}.\-]+\.\p{L}{2,}',
    unicode: true,
  ),
  // JWT benzeri: üç base64url parçası.
  RegExp(r'eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+'),
  // IPv4 (IPv6'dan önce: '::ffff:1.2.3.4' eşlemeli yazım da maskelenir).
  RegExp(r'\b(?:\d{1,3}\.){3}\d{1,3}\b'),
  // IPv6: tam yazım (8 grup) ya da '::' içeren sıkıştırılmış yazım. Saat (12:30:45) eşleşmez.
  RegExp(
    r'(?<![0-9A-Fa-f:])(?:(?:[0-9A-Fa-f]{1,4}:){7}[0-9A-Fa-f]{1,4}'
    r'|(?:[0-9A-Fa-f]{1,4}:){0,6}[0-9A-Fa-f]{0,4}::(?:[0-9A-Fa-f]{1,4}:){0,6}[0-9A-Fa-f]{0,4})'
    r'(?![0-9A-Fa-f:])',
  ),
  // 8+ haneli sayı dizileri.
  RegExp(r'\d{8,}'),
];

/// `SqliteException.toString()` buradan sonra SQL ifadesini ve parametrelerini (not, başlık gibi serbest
/// metin) ekler; Drift sarmalayıcıları bu metni taşır.
const _sqlStatementMarker = 'Causing statement';

/// SQL ifadesi/parametrelerini keser; e-posta, IPv4/IPv6, JWT benzeri ve 8+ haneli sayıları
/// `[redacted]` ile değiştirir.
String maskPii(String text) {
  final cut = text.indexOf(_sqlStatementMarker);
  final head = cut < 0 ? text : text.substring(0, cut).trimRight();
  return _piiPatterns.fold(head, (t, p) => t.replaceAll(p, _redacted));
}

String? _mask(String? text) => text == null ? null : maskPii(text);

/// `beforeSend` süzgeci: yalnız izinli alanları taşıyan yeni bir olay döndürür.
/// `user`, `request`, `extra`, sunucu adı, modüller, `transaction` ve izinsiz bağlamlar/etiketler
/// kopyalanmaz; iş parçacıklarından yalnız kimlik/durum ve süzülmüş yığın kalır.
SentryEvent? scrubEvent(SentryEvent event) => SentryEvent(
  eventId: event.eventId,
  timestamp: event.timestamp,
  throwable: event.throwableMechanism,
  platform: event.platform,
  sdk: event.sdk,
  debugMeta: event.debugMeta,
  type: event.type,
  level: event.level,
  logger: event.logger,
  release: event.release,
  dist: event.dist,
  environment: event.environment,
  fingerprint: event.fingerprint,
  message: event.message == null
      ? null
      : SentryMessage(maskPii(event.message!.formatted)),
  exceptions: event.exceptions?.map(_scrubException).toList(),
  threads: event.threads
      ?.map(
        (t) => SentryThread(
          id: t.id,
          crashed: t.crashed,
          current: t.current,
          stacktrace: _scrubStackTrace(t.stacktrace),
        ),
      )
      .toList(),
  tags: event.tags == null
      ? null
      : {
          for (final e in event.tags!.entries)
            if (_allowedTags.contains(e.key)) e.key: maskPii(e.value),
        },
  contexts: _scrubContexts(event.contexts),
  breadcrumbs: event.breadcrumbs
      ?.map(scrubBreadcrumb)
      .whereType<Breadcrumb>()
      .toList(),
);

/// `beforeBreadcrumb` süzgeci: `nirengi.` önekli değilse atılır; mesaj maskelenir, veri atılır.
Breadcrumb? scrubBreadcrumb(Breadcrumb? crumb) {
  if (crumb == null ||
      !(crumb.category?.startsWith(breadcrumbCategoryPrefix) ?? false)) {
    return null;
  }
  return Breadcrumb(
    category: crumb.category,
    message: _mask(crumb.message),
    timestamp: crumb.timestamp,
    level: crumb.level,
    type: crumb.type,
  );
}

SentryException _scrubException(SentryException ex) {
  final mechanism = ex.mechanism;
  return SentryException(
    type: ex.type,
    value: _mask(ex.value),
    module: ex.module,
    threadId: ex.threadId,
    stackTrace: _scrubStackTrace(ex.stackTrace),
    mechanism: mechanism == null
        ? null
        : Mechanism(
            type: mechanism.type,
            handled: mechanism.handled,
            synthetic: mechanism.synthetic,
            exceptionId: mechanism.exceptionId,
            parentId: mechanism.parentId,
            isExceptionGroup: mechanism.isExceptionGroup,
            source: mechanism.source,
          ),
  );
}

/// Çerçevelerden yalnız konum alanları kalır; `vars`, kaynak satırları (`contextLine` vb.) atılır.
SentryStackTrace? _scrubStackTrace(SentryStackTrace? st) => st == null
    ? null
    : SentryStackTrace(
        frames: [
          for (final f in st.frames)
            SentryStackFrame(
              module: f.module,
              function: f.function,
              fileName: f.fileName,
              absPath: f.absPath,
              lineNo: f.lineNo,
              colNo: f.colNo,
              inApp: f.inApp,
              package: f.package,
              platform: f.platform,
              native: f.native,
              instructionAddr: f.instructionAddr,
              symbolAddr: f.symbolAddr,
              imageAddr: f.imageAddr,
            ),
        ],
      );

/// Yalnız runtime, işletim sistemi ve uygulama bağlamı kalır; cihaz ve özel bağlamlar atılır.
Contexts _scrubContexts(Contexts c) {
  final os = c.operatingSystem;
  final app = c.app;
  return Contexts(
    operatingSystem: os == null
        ? null
        : SentryOperatingSystem(
            name: os.name,
            version: os.version,
            build: os.build,
          ),
    runtimes: [
      for (final r in c.runtimes)
        SentryRuntime(name: r.name, version: r.version, compiler: r.compiler),
    ],
    app: app == null
        ? null
        : SentryApp(
            name: app.name,
            version: app.version,
            build: app.build,
            buildType: app.buildType,
            identifier: app.identifier,
            inForeground: app.inForeground,
          ),
  );
}
