/// Edit time for last-writer-wins records: the server keeps the stored copy unless the incoming
/// `clientModifiedAt` is strictly newer, so a write never goes below the stored value + 1 microsecond
/// (a lagging device clock or a record written ahead by another device would silently lose the edit).
int nextEditMicros(DateTime now, int? storedMicros) {
  final nowMicros = now.toUtc().microsecondsSinceEpoch;
  return storedMicros != null && storedMicros >= nowMicros
      ? storedMicros + 1
      : nowMicros;
}
