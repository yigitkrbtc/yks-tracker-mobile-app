/// Downloads the student's data as a ZIP of CSVs and hands it to the OS share sheet (K13).
abstract interface class DataExportService {
  /// Throws [DataExportException].
  Future<void> exportAndShare();
}

class DataExportException implements Exception {
  const DataExportException(this.message);

  final String message;

  @override
  String toString() => 'DataExportException($message)';
}
