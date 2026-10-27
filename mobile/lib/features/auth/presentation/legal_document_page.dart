import 'package:flutter/material.dart';

import '../../../core/config/app_config.dart';
import '../domain/legal_documents.dart';

/// Renders the bundled legal texts. They use a small Markdown subset: `#`/`###` headings,
/// `- ` bullets and `**bold**` labels; `{app}` is replaced with the product name.
class LegalDocumentPage extends StatelessWidget {
  const LegalDocumentPage({super.key, required this.document});

  final LegalDocument document;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(document.title)),
      body: FutureBuilder<String>(
        future: DefaultAssetBundle.of(context).loadString(document.asset),
        builder: (context, snapshot) {
          final text = snapshot.data;
          if (text == null) {
            return const Center(child: CircularProgressIndicator());
          }
          return ListView(
            padding: const EdgeInsets.all(20),
            children: [
              const _DraftNotice(),
              ...LegalMarkdown.parse(
                text.replaceAll('{app}', AppConfig.appName),
              ).map((block) => block.build(context)),
            ],
          );
        },
      ),
    );
  }
}

class _DraftNotice extends StatelessWidget {
  const _DraftNotice();

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Card(
      color: scheme.tertiaryContainer,
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Text(
          'Taslak metin: yayından önce hukuki incelemeden geçecek.',
          style: TextStyle(color: scheme.onTertiaryContainer),
        ),
      ),
    );
  }
}

sealed class LegalBlock {
  const LegalBlock(this.text);

  final String text;

  Widget build(BuildContext context);
}

class LegalHeading extends LegalBlock {
  const LegalHeading(super.text, {required this.level});

  final int level;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context).textTheme;
    return Padding(
      padding: const EdgeInsets.only(top: 16, bottom: 8),
      child: Text(
        text,
        style: level == 1 ? theme.headlineSmall : theme.titleMedium,
      ),
    );
  }
}

class LegalParagraph extends LegalBlock {
  const LegalParagraph(super.text, {this.bullet = false});

  final bool bullet;

  @override
  Widget build(BuildContext context) {
    final body = Text.rich(TextSpan(children: LegalMarkdown.inline(text)));
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: bullet
          ? Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text('•  '),
                Expanded(child: body),
              ],
            )
          : body,
    );
  }
}

abstract final class LegalMarkdown {
  static List<LegalBlock> parse(String source) => [
    for (final line in source.split('\n').map((l) => l.trim()))
      if (line.startsWith('### '))
        LegalHeading(line.substring(4), level: 3)
      else if (line.startsWith('# '))
        LegalHeading(line.substring(2), level: 1)
      else if (line.startsWith('- '))
        LegalParagraph(line.substring(2), bullet: true)
      else if (line.isNotEmpty)
        LegalParagraph(line),
  ];

  static List<TextSpan> inline(String text) {
    final parts = text.split('**');
    return [
      for (var i = 0; i < parts.length; i++)
        TextSpan(
          text: parts[i],
          style: i.isOdd ? const TextStyle(fontWeight: FontWeight.bold) : null,
        ),
    ];
  }
}
