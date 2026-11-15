import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Header the API reads to refuse app versions it no longer supports (ADR-0021 M4).
const clientVersionHeader = 'X-Client-Version';

/// The installed app version (e.g. `1.2.0`), read once at start-up from the platform and overridden in `main`.
/// `null` (tests) sends no header, which the API lets through.
final clientVersionProvider = Provider<String?>((ref) => null);

/// True once the API answered 426: this app version is too old to be served. The shell then shows the
/// "update required" screen and keeps it, since nothing short of an update clears the condition.
final updateRequiredProvider = NotifierProvider<UpdateRequired, bool>(
  UpdateRequired.new,
);

class UpdateRequired extends Notifier<bool> {
  @override
  bool build() => false;

  void markRequired() => state = true;
}

/// Sends [clientVersionHeader] on every request and reports a 426 through [onUpgradeRequired].
class ClientVersionInterceptor extends Interceptor {
  ClientVersionInterceptor(this._version, this._onUpgradeRequired);

  final String? _version;
  final void Function() _onUpgradeRequired;

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    final version = _version;
    if (version != null) {
      options.headers[clientVersionHeader] = version;
    }
    handler.next(options);
  }

  @override
  void onError(DioException err, ErrorInterceptorHandler handler) {
    if (err.response?.statusCode == 426) {
      _onUpgradeRequired();
    }
    handler.next(err);
  }
}
