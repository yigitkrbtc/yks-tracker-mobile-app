import 'dart:io';

import 'package:dio/dio.dart';
import 'package:path_provider/path_provider.dart';
import 'package:share_plus/share_plus.dart';

import '../../../core/network/api_problem.dart';
import '../domain/data_export.dart';

/// GET /v1/me/export into the temporary folder, then the share sheet. The file is overwritten next time.
class HttpDataExportService implements DataExportService {
  HttpDataExportService(this._dio);

  static const fallbackName = 'nirengi-verilerim.zip';

  final Dio _dio;

  @override
  Future<void> exportAndShare() async {
    final Response<List<int>> response;
    try {
      response = await _dio.get<List<int>>(
        '/v1/me/export',
        options: Options(responseType: ResponseType.bytes),
      );
    } on DioException catch (e) {
      final problem = ApiProblem.fromDio(e);
      throw DataExportException(switch (problem.status) {
        0 => 'Sunucuya ulaşılamadı; internete bağlanınca tekrar dene.',
        429 =>
          'Bir saatte en çok 3 kez indirebilirsin; biraz sonra tekrar dene.',
        _ => 'Veriler indirilemedi; tekrar dene.',
      });
    }
    final name = fileName(response.headers.value('content-disposition'));
    final dir = await getTemporaryDirectory();
    final file = File('${dir.path}/$name');
    await file.writeAsBytes(response.data!, flush: true);
    await SharePlus.instance.share(
      ShareParams(
        files: [XFile(file.path, mimeType: 'application/zip')],
        subject: 'Nirengi verilerim',
      ),
    );
  }

  /// The server's file name (nirengi-verilerim-2026-11-01.zip), reduced to safe characters.
  static String fileName(String? disposition) {
    final match = RegExp(r'filename="?([^";]+)"?')
        .firstMatch(disposition ?? '');
    final name = match
        ?.group(1)
        ?.replaceAll(RegExp(r'[^A-Za-z0-9._-]'), '')
        .replaceFirst(RegExp(r'^\.+'), '');
    return name == null || name.isEmpty || !name.endsWith('.zip')
        ? fallbackName
        : name;
  }
}
