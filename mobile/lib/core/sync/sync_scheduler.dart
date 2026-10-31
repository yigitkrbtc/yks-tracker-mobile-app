import 'dart:async';

import 'package:flutter/widgets.dart';

import 'outbox_store.dart';
import 'sync_service.dart';

/// Decides when to sync while a student is signed in: shortly after a local write, when the app comes to the
/// foreground, every few minutes, and when a backoff retry is due. The app starts and stops it with the session.
class SyncScheduler {
  SyncScheduler(this._service, this._outbox);

  static const writeDelay = Duration(milliseconds: 500);
  static const period = Duration(minutes: 5);

  final SyncService _service;
  final OutboxStore _outbox;

  StreamSubscription<void>? _writes;
  Timer? _debounce;
  Timer? _periodic;
  Timer? _retry;
  AppLifecycleListener? _lifecycle;

  bool get isRunning => _periodic != null;

  void start() {
    if (isRunning) {
      return;
    }
    _writes = _outbox.changes.listen((_) {
      _debounce?.cancel();
      _debounce = Timer(writeDelay, _run);
    });
    _periodic = Timer.periodic(period, (_) => _run());
    _lifecycle = AppLifecycleListener(onResume: _run);
    _run();
  }

  /// Runs now, ignoring the backoff (pull-to-refresh).
  Future<SyncOutcome> syncNow() => _after(_service.sync(force: true));

  void stop() {
    _writes?.cancel();
    _debounce?.cancel();
    _periodic?.cancel();
    _retry?.cancel();
    _lifecycle?.dispose();
    _writes = null;
    _debounce = _periodic = _retry = null;
    _lifecycle = null;
    _service.reset();
  }

  void _run() => _after(_service.sync());

  Future<SyncOutcome> _after(Future<SyncOutcome> run) async {
    final outcome = await run;
    final retryAt = _service.retryAt;
    if (isRunning && outcome == SyncOutcome.offline && retryAt != null) {
      _retry?.cancel();
      _retry = Timer(retryAt.difference(DateTime.now()), _run);
    }
    return outcome;
  }
}
