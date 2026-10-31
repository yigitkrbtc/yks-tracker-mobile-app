const _months = [
  'Ocak',
  'Şubat',
  'Mart',
  'Nisan',
  'Mayıs',
  'Haziran',
  'Temmuz',
  'Ağustos',
  'Eylül',
  'Ekim',
  'Kasım',
  'Aralık',
];

/// "31 Ekim 2026".
String formatDay(DateTime d) => '${d.day} ${_months[d.month - 1]} ${d.year}';

/// Net with a decimal comma and no trailing zeros: 11,25 / 11,5 / 11.
String formatNet(double net) => net
    .toStringAsFixed(2)
    .replaceAll(RegExp(r'\.?0+$'), '')
    .replaceAll('.', ',');
