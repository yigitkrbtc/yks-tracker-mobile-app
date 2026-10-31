import 'package:flutter_test/flutter_test.dart';
import 'package:nirengi/features/topic_tests/domain/topic_test.dart';

void main() {
  final now = DateTime.utc(2026, 10, 31, 22); // 01:00 on 1 Nov in Türkiye

  TopicTest sample({
    DateTime? on,
    String? sourceId = 'src',
    String? sourceName,
    List<TopicResult>? results,
    int? duration,
  }) => TopicTest(
    id: 't1',
    studiedOn: on ?? DateTime.utc(2026, 11, 1),
    sourceId: sourceId,
    sourceName: sourceName,
    durationMinutes: duration,
    results:
        results ??
        const [TopicResult(topicId: 'a', total: 20, correct: 15, wrong: 4)],
    modifiedAt: now,
  );

  test('net takes a quarter per wrong answer; blank is what is left', () {
    const r = TopicResult(topicId: 'a', total: 20, correct: 15, wrong: 4);
    expect(r.blank, 1);
    expect(r.net, 14);
    final t = sample(
      results: const [
        TopicResult(topicId: 'a', total: 20, correct: 15, wrong: 4),
        TopicResult(topicId: 'b', total: 10, correct: 5, wrong: 1),
      ],
    );
    expect((t.total, t.correct, t.wrong, t.blank), (30, 20, 5, 5));
    expect(t.net, 18.75);
  });

  test('today follows Türkiye time and the date window is 365 days', () {
    expect(TopicTestRules.todayInTurkey(now), DateTime.utc(2026, 11, 1));
    expect(TopicTestRules.validate(sample(), now), isEmpty);
    expect(
      TopicTestRules.validate(sample(on: DateTime.utc(2026, 11, 2)), now),
      ['Tarih bugün ile son 365 gün arasında olmalı.'],
    );
    expect(
      TopicTestRules.validate(sample(on: DateTime.utc(2025, 11, 1)), now),
      isEmpty,
    );
    expect(
      TopicTestRules.validate(sample(on: DateTime.utc(2025, 10, 31)), now),
      hasLength(1),
    );
  });

  test('exactly one of listed source or typed name', () {
    expect(TopicTestRules.validate(sample(sourceId: null), now), [
      'Listeden bir kaynak seç ya da kaynağın adını yaz.',
    ]);
    expect(
      TopicTestRules.validate(
        sample(sourceId: null, sourceName: 'Ders notu'),
        now,
      ),
      isEmpty,
    );
    expect(
      TopicTestRules.validate(sample(sourceName: 'Ders notu'), now),
      hasLength(1),
    );
  });

  test('answer counts, reasons, topics and duration are bounded', () {
    List<String> errors(List<TopicResult> results, {int? duration}) =>
        TopicTestRules.validate(
          sample(results: results, duration: duration),
          now,
        );

    expect(errors(const []), ['Bir testte 1 ile 20 arasında konu olur.']);
    expect(
      errors(const [TopicResult(topicId: 'a', total: 5, correct: 4, wrong: 2)]),
      ['Doğru ve yanlış toplamı soru sayısını aşamaz.'],
    );
    expect(
      errors(const [TopicResult(topicId: 'a', total: 0, correct: 0, wrong: 0)]),
      ['Soru sayısı 1 ile 200 arasında olmalı.'],
    );
    expect(
      errors(const [
        TopicResult(
          topicId: 'a',
          total: 10,
          correct: 5,
          wrong: 2,
          mistakeKnowledge: 2,
          mistakeTime: 1,
        ),
      ]),
      ['Yanlış nedenlerinin toplamı yanlış sayısını aşamaz.'],
    );
    expect(
      errors(const [
        TopicResult(topicId: 'a', total: 10, correct: 5, wrong: 2),
        TopicResult(topicId: 'a', total: 10, correct: 5, wrong: 2),
      ]),
      ['Bir konu aynı testte iki kez olamaz.'],
    );
    expect(
      errors(const [
        TopicResult(topicId: 'a', total: 10, correct: 5, wrong: 2),
      ], duration: 601),
      ['Süre 1 ile 600 dakika arasında olmalı.'],
    );
  });
}
