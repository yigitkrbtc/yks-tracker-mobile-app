// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'app_database.dart';

// ignore_for_file: type=lint
class $TopicTestsTable extends TopicTests
    with TableInfo<$TopicTestsTable, TopicTestRow> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $TopicTestsTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _idMeta = const VerificationMeta('id');
  @override
  late final GeneratedColumn<String> id = GeneratedColumn<String>(
    'id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _studiedOnMeta = const VerificationMeta(
    'studiedOn',
  );
  @override
  late final GeneratedColumn<String> studiedOn = GeneratedColumn<String>(
    'studied_on',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _sourceIdMeta = const VerificationMeta(
    'sourceId',
  );
  @override
  late final GeneratedColumn<String> sourceId = GeneratedColumn<String>(
    'source_id',
    aliasedName,
    true,
    type: DriftSqlType.string,
    requiredDuringInsert: false,
  );
  static const VerificationMeta _sourceNameMeta = const VerificationMeta(
    'sourceName',
  );
  @override
  late final GeneratedColumn<String> sourceName = GeneratedColumn<String>(
    'source_name',
    aliasedName,
    true,
    type: DriftSqlType.string,
    requiredDuringInsert: false,
  );
  static const VerificationMeta _isRepeatMeta = const VerificationMeta(
    'isRepeat',
  );
  @override
  late final GeneratedColumn<bool> isRepeat = GeneratedColumn<bool>(
    'is_repeat',
    aliasedName,
    false,
    type: DriftSqlType.bool,
    requiredDuringInsert: true,
    defaultConstraints: GeneratedColumn.constraintIsAlways(
      'CHECK ("is_repeat" IN (0, 1))',
    ),
  );
  static const VerificationMeta _difficultyMeta = const VerificationMeta(
    'difficulty',
  );
  @override
  late final GeneratedColumn<String> difficulty = GeneratedColumn<String>(
    'difficulty',
    aliasedName,
    true,
    type: DriftSqlType.string,
    requiredDuringInsert: false,
  );
  static const VerificationMeta _durationMinutesMeta = const VerificationMeta(
    'durationMinutes',
  );
  @override
  late final GeneratedColumn<int> durationMinutes = GeneratedColumn<int>(
    'duration_minutes',
    aliasedName,
    true,
    type: DriftSqlType.int,
    requiredDuringInsert: false,
  );
  static const VerificationMeta _noteMeta = const VerificationMeta('note');
  @override
  late final GeneratedColumn<String> note = GeneratedColumn<String>(
    'note',
    aliasedName,
    true,
    type: DriftSqlType.string,
    requiredDuringInsert: false,
  );
  static const VerificationMeta _clientModifiedAtMeta = const VerificationMeta(
    'clientModifiedAt',
  );
  @override
  late final GeneratedColumn<int> clientModifiedAt = GeneratedColumn<int>(
    'client_modified_at',
    aliasedName,
    false,
    type: DriftSqlType.int,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _deletedMeta = const VerificationMeta(
    'deleted',
  );
  @override
  late final GeneratedColumn<bool> deleted = GeneratedColumn<bool>(
    'deleted',
    aliasedName,
    false,
    type: DriftSqlType.bool,
    requiredDuringInsert: false,
    defaultConstraints: GeneratedColumn.constraintIsAlways(
      'CHECK ("deleted" IN (0, 1))',
    ),
    defaultValue: const Constant(false),
  );
  static const VerificationMeta _changeSeqMeta = const VerificationMeta(
    'changeSeq',
  );
  @override
  late final GeneratedColumn<int> changeSeq = GeneratedColumn<int>(
    'change_seq',
    aliasedName,
    false,
    type: DriftSqlType.int,
    requiredDuringInsert: false,
    defaultValue: const Constant(0),
  );
  @override
  List<GeneratedColumn> get $columns => [
    id,
    studiedOn,
    sourceId,
    sourceName,
    isRepeat,
    difficulty,
    durationMinutes,
    note,
    clientModifiedAt,
    deleted,
    changeSeq,
  ];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'topic_tests';
  @override
  VerificationContext validateIntegrity(
    Insertable<TopicTestRow> instance, {
    bool isInserting = false,
  }) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('id')) {
      context.handle(_idMeta, id.isAcceptableOrUnknown(data['id']!, _idMeta));
    } else if (isInserting) {
      context.missing(_idMeta);
    }
    if (data.containsKey('studied_on')) {
      context.handle(
        _studiedOnMeta,
        studiedOn.isAcceptableOrUnknown(data['studied_on']!, _studiedOnMeta),
      );
    } else if (isInserting) {
      context.missing(_studiedOnMeta);
    }
    if (data.containsKey('source_id')) {
      context.handle(
        _sourceIdMeta,
        sourceId.isAcceptableOrUnknown(data['source_id']!, _sourceIdMeta),
      );
    }
    if (data.containsKey('source_name')) {
      context.handle(
        _sourceNameMeta,
        sourceName.isAcceptableOrUnknown(data['source_name']!, _sourceNameMeta),
      );
    }
    if (data.containsKey('is_repeat')) {
      context.handle(
        _isRepeatMeta,
        isRepeat.isAcceptableOrUnknown(data['is_repeat']!, _isRepeatMeta),
      );
    } else if (isInserting) {
      context.missing(_isRepeatMeta);
    }
    if (data.containsKey('difficulty')) {
      context.handle(
        _difficultyMeta,
        difficulty.isAcceptableOrUnknown(data['difficulty']!, _difficultyMeta),
      );
    }
    if (data.containsKey('duration_minutes')) {
      context.handle(
        _durationMinutesMeta,
        durationMinutes.isAcceptableOrUnknown(
          data['duration_minutes']!,
          _durationMinutesMeta,
        ),
      );
    }
    if (data.containsKey('note')) {
      context.handle(
        _noteMeta,
        note.isAcceptableOrUnknown(data['note']!, _noteMeta),
      );
    }
    if (data.containsKey('client_modified_at')) {
      context.handle(
        _clientModifiedAtMeta,
        clientModifiedAt.isAcceptableOrUnknown(
          data['client_modified_at']!,
          _clientModifiedAtMeta,
        ),
      );
    } else if (isInserting) {
      context.missing(_clientModifiedAtMeta);
    }
    if (data.containsKey('deleted')) {
      context.handle(
        _deletedMeta,
        deleted.isAcceptableOrUnknown(data['deleted']!, _deletedMeta),
      );
    }
    if (data.containsKey('change_seq')) {
      context.handle(
        _changeSeqMeta,
        changeSeq.isAcceptableOrUnknown(data['change_seq']!, _changeSeqMeta),
      );
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {id};
  @override
  TopicTestRow map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return TopicTestRow(
      id: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}id'],
      )!,
      studiedOn: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}studied_on'],
      )!,
      sourceId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}source_id'],
      ),
      sourceName: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}source_name'],
      ),
      isRepeat: attachedDatabase.typeMapping.read(
        DriftSqlType.bool,
        data['${effectivePrefix}is_repeat'],
      )!,
      difficulty: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}difficulty'],
      ),
      durationMinutes: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}duration_minutes'],
      ),
      note: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}note'],
      ),
      clientModifiedAt: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}client_modified_at'],
      )!,
      deleted: attachedDatabase.typeMapping.read(
        DriftSqlType.bool,
        data['${effectivePrefix}deleted'],
      )!,
      changeSeq: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}change_seq'],
      )!,
    );
  }

  @override
  $TopicTestsTable createAlias(String alias) {
    return $TopicTestsTable(attachedDatabase, alias);
  }
}

class TopicTestRow extends DataClass implements Insertable<TopicTestRow> {
  final String id;
  final String studiedOn;
  final String? sourceId;
  final String? sourceName;
  final bool isRepeat;
  final String? difficulty;
  final int? durationMinutes;
  final String? note;

  /// Microseconds since epoch, UTC; the server keeps the newest edit by this time.
  final int clientModifiedAt;

  /// Hidden from the screens, waiting for the delete to reach the server.
  final bool deleted;

  /// Position in the server's change feed; 0 until the server has confirmed it.
  final int changeSeq;
  const TopicTestRow({
    required this.id,
    required this.studiedOn,
    this.sourceId,
    this.sourceName,
    required this.isRepeat,
    this.difficulty,
    this.durationMinutes,
    this.note,
    required this.clientModifiedAt,
    required this.deleted,
    required this.changeSeq,
  });
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['id'] = Variable<String>(id);
    map['studied_on'] = Variable<String>(studiedOn);
    if (!nullToAbsent || sourceId != null) {
      map['source_id'] = Variable<String>(sourceId);
    }
    if (!nullToAbsent || sourceName != null) {
      map['source_name'] = Variable<String>(sourceName);
    }
    map['is_repeat'] = Variable<bool>(isRepeat);
    if (!nullToAbsent || difficulty != null) {
      map['difficulty'] = Variable<String>(difficulty);
    }
    if (!nullToAbsent || durationMinutes != null) {
      map['duration_minutes'] = Variable<int>(durationMinutes);
    }
    if (!nullToAbsent || note != null) {
      map['note'] = Variable<String>(note);
    }
    map['client_modified_at'] = Variable<int>(clientModifiedAt);
    map['deleted'] = Variable<bool>(deleted);
    map['change_seq'] = Variable<int>(changeSeq);
    return map;
  }

  TopicTestsCompanion toCompanion(bool nullToAbsent) {
    return TopicTestsCompanion(
      id: Value(id),
      studiedOn: Value(studiedOn),
      sourceId: sourceId == null && nullToAbsent
          ? const Value.absent()
          : Value(sourceId),
      sourceName: sourceName == null && nullToAbsent
          ? const Value.absent()
          : Value(sourceName),
      isRepeat: Value(isRepeat),
      difficulty: difficulty == null && nullToAbsent
          ? const Value.absent()
          : Value(difficulty),
      durationMinutes: durationMinutes == null && nullToAbsent
          ? const Value.absent()
          : Value(durationMinutes),
      note: note == null && nullToAbsent ? const Value.absent() : Value(note),
      clientModifiedAt: Value(clientModifiedAt),
      deleted: Value(deleted),
      changeSeq: Value(changeSeq),
    );
  }

  factory TopicTestRow.fromJson(
    Map<String, dynamic> json, {
    ValueSerializer? serializer,
  }) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return TopicTestRow(
      id: serializer.fromJson<String>(json['id']),
      studiedOn: serializer.fromJson<String>(json['studiedOn']),
      sourceId: serializer.fromJson<String?>(json['sourceId']),
      sourceName: serializer.fromJson<String?>(json['sourceName']),
      isRepeat: serializer.fromJson<bool>(json['isRepeat']),
      difficulty: serializer.fromJson<String?>(json['difficulty']),
      durationMinutes: serializer.fromJson<int?>(json['durationMinutes']),
      note: serializer.fromJson<String?>(json['note']),
      clientModifiedAt: serializer.fromJson<int>(json['clientModifiedAt']),
      deleted: serializer.fromJson<bool>(json['deleted']),
      changeSeq: serializer.fromJson<int>(json['changeSeq']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'id': serializer.toJson<String>(id),
      'studiedOn': serializer.toJson<String>(studiedOn),
      'sourceId': serializer.toJson<String?>(sourceId),
      'sourceName': serializer.toJson<String?>(sourceName),
      'isRepeat': serializer.toJson<bool>(isRepeat),
      'difficulty': serializer.toJson<String?>(difficulty),
      'durationMinutes': serializer.toJson<int?>(durationMinutes),
      'note': serializer.toJson<String?>(note),
      'clientModifiedAt': serializer.toJson<int>(clientModifiedAt),
      'deleted': serializer.toJson<bool>(deleted),
      'changeSeq': serializer.toJson<int>(changeSeq),
    };
  }

  TopicTestRow copyWith({
    String? id,
    String? studiedOn,
    Value<String?> sourceId = const Value.absent(),
    Value<String?> sourceName = const Value.absent(),
    bool? isRepeat,
    Value<String?> difficulty = const Value.absent(),
    Value<int?> durationMinutes = const Value.absent(),
    Value<String?> note = const Value.absent(),
    int? clientModifiedAt,
    bool? deleted,
    int? changeSeq,
  }) => TopicTestRow(
    id: id ?? this.id,
    studiedOn: studiedOn ?? this.studiedOn,
    sourceId: sourceId.present ? sourceId.value : this.sourceId,
    sourceName: sourceName.present ? sourceName.value : this.sourceName,
    isRepeat: isRepeat ?? this.isRepeat,
    difficulty: difficulty.present ? difficulty.value : this.difficulty,
    durationMinutes: durationMinutes.present
        ? durationMinutes.value
        : this.durationMinutes,
    note: note.present ? note.value : this.note,
    clientModifiedAt: clientModifiedAt ?? this.clientModifiedAt,
    deleted: deleted ?? this.deleted,
    changeSeq: changeSeq ?? this.changeSeq,
  );
  TopicTestRow copyWithCompanion(TopicTestsCompanion data) {
    return TopicTestRow(
      id: data.id.present ? data.id.value : this.id,
      studiedOn: data.studiedOn.present ? data.studiedOn.value : this.studiedOn,
      sourceId: data.sourceId.present ? data.sourceId.value : this.sourceId,
      sourceName: data.sourceName.present
          ? data.sourceName.value
          : this.sourceName,
      isRepeat: data.isRepeat.present ? data.isRepeat.value : this.isRepeat,
      difficulty: data.difficulty.present
          ? data.difficulty.value
          : this.difficulty,
      durationMinutes: data.durationMinutes.present
          ? data.durationMinutes.value
          : this.durationMinutes,
      note: data.note.present ? data.note.value : this.note,
      clientModifiedAt: data.clientModifiedAt.present
          ? data.clientModifiedAt.value
          : this.clientModifiedAt,
      deleted: data.deleted.present ? data.deleted.value : this.deleted,
      changeSeq: data.changeSeq.present ? data.changeSeq.value : this.changeSeq,
    );
  }

  @override
  String toString() {
    return (StringBuffer('TopicTestRow(')
          ..write('id: $id, ')
          ..write('studiedOn: $studiedOn, ')
          ..write('sourceId: $sourceId, ')
          ..write('sourceName: $sourceName, ')
          ..write('isRepeat: $isRepeat, ')
          ..write('difficulty: $difficulty, ')
          ..write('durationMinutes: $durationMinutes, ')
          ..write('note: $note, ')
          ..write('clientModifiedAt: $clientModifiedAt, ')
          ..write('deleted: $deleted, ')
          ..write('changeSeq: $changeSeq')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(
    id,
    studiedOn,
    sourceId,
    sourceName,
    isRepeat,
    difficulty,
    durationMinutes,
    note,
    clientModifiedAt,
    deleted,
    changeSeq,
  );
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is TopicTestRow &&
          other.id == this.id &&
          other.studiedOn == this.studiedOn &&
          other.sourceId == this.sourceId &&
          other.sourceName == this.sourceName &&
          other.isRepeat == this.isRepeat &&
          other.difficulty == this.difficulty &&
          other.durationMinutes == this.durationMinutes &&
          other.note == this.note &&
          other.clientModifiedAt == this.clientModifiedAt &&
          other.deleted == this.deleted &&
          other.changeSeq == this.changeSeq);
}

class TopicTestsCompanion extends UpdateCompanion<TopicTestRow> {
  final Value<String> id;
  final Value<String> studiedOn;
  final Value<String?> sourceId;
  final Value<String?> sourceName;
  final Value<bool> isRepeat;
  final Value<String?> difficulty;
  final Value<int?> durationMinutes;
  final Value<String?> note;
  final Value<int> clientModifiedAt;
  final Value<bool> deleted;
  final Value<int> changeSeq;
  final Value<int> rowid;
  const TopicTestsCompanion({
    this.id = const Value.absent(),
    this.studiedOn = const Value.absent(),
    this.sourceId = const Value.absent(),
    this.sourceName = const Value.absent(),
    this.isRepeat = const Value.absent(),
    this.difficulty = const Value.absent(),
    this.durationMinutes = const Value.absent(),
    this.note = const Value.absent(),
    this.clientModifiedAt = const Value.absent(),
    this.deleted = const Value.absent(),
    this.changeSeq = const Value.absent(),
    this.rowid = const Value.absent(),
  });
  TopicTestsCompanion.insert({
    required String id,
    required String studiedOn,
    this.sourceId = const Value.absent(),
    this.sourceName = const Value.absent(),
    required bool isRepeat,
    this.difficulty = const Value.absent(),
    this.durationMinutes = const Value.absent(),
    this.note = const Value.absent(),
    required int clientModifiedAt,
    this.deleted = const Value.absent(),
    this.changeSeq = const Value.absent(),
    this.rowid = const Value.absent(),
  }) : id = Value(id),
       studiedOn = Value(studiedOn),
       isRepeat = Value(isRepeat),
       clientModifiedAt = Value(clientModifiedAt);
  static Insertable<TopicTestRow> custom({
    Expression<String>? id,
    Expression<String>? studiedOn,
    Expression<String>? sourceId,
    Expression<String>? sourceName,
    Expression<bool>? isRepeat,
    Expression<String>? difficulty,
    Expression<int>? durationMinutes,
    Expression<String>? note,
    Expression<int>? clientModifiedAt,
    Expression<bool>? deleted,
    Expression<int>? changeSeq,
    Expression<int>? rowid,
  }) {
    return RawValuesInsertable({
      if (id != null) 'id': id,
      if (studiedOn != null) 'studied_on': studiedOn,
      if (sourceId != null) 'source_id': sourceId,
      if (sourceName != null) 'source_name': sourceName,
      if (isRepeat != null) 'is_repeat': isRepeat,
      if (difficulty != null) 'difficulty': difficulty,
      if (durationMinutes != null) 'duration_minutes': durationMinutes,
      if (note != null) 'note': note,
      if (clientModifiedAt != null) 'client_modified_at': clientModifiedAt,
      if (deleted != null) 'deleted': deleted,
      if (changeSeq != null) 'change_seq': changeSeq,
      if (rowid != null) 'rowid': rowid,
    });
  }

  TopicTestsCompanion copyWith({
    Value<String>? id,
    Value<String>? studiedOn,
    Value<String?>? sourceId,
    Value<String?>? sourceName,
    Value<bool>? isRepeat,
    Value<String?>? difficulty,
    Value<int?>? durationMinutes,
    Value<String?>? note,
    Value<int>? clientModifiedAt,
    Value<bool>? deleted,
    Value<int>? changeSeq,
    Value<int>? rowid,
  }) {
    return TopicTestsCompanion(
      id: id ?? this.id,
      studiedOn: studiedOn ?? this.studiedOn,
      sourceId: sourceId ?? this.sourceId,
      sourceName: sourceName ?? this.sourceName,
      isRepeat: isRepeat ?? this.isRepeat,
      difficulty: difficulty ?? this.difficulty,
      durationMinutes: durationMinutes ?? this.durationMinutes,
      note: note ?? this.note,
      clientModifiedAt: clientModifiedAt ?? this.clientModifiedAt,
      deleted: deleted ?? this.deleted,
      changeSeq: changeSeq ?? this.changeSeq,
      rowid: rowid ?? this.rowid,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (id.present) {
      map['id'] = Variable<String>(id.value);
    }
    if (studiedOn.present) {
      map['studied_on'] = Variable<String>(studiedOn.value);
    }
    if (sourceId.present) {
      map['source_id'] = Variable<String>(sourceId.value);
    }
    if (sourceName.present) {
      map['source_name'] = Variable<String>(sourceName.value);
    }
    if (isRepeat.present) {
      map['is_repeat'] = Variable<bool>(isRepeat.value);
    }
    if (difficulty.present) {
      map['difficulty'] = Variable<String>(difficulty.value);
    }
    if (durationMinutes.present) {
      map['duration_minutes'] = Variable<int>(durationMinutes.value);
    }
    if (note.present) {
      map['note'] = Variable<String>(note.value);
    }
    if (clientModifiedAt.present) {
      map['client_modified_at'] = Variable<int>(clientModifiedAt.value);
    }
    if (deleted.present) {
      map['deleted'] = Variable<bool>(deleted.value);
    }
    if (changeSeq.present) {
      map['change_seq'] = Variable<int>(changeSeq.value);
    }
    if (rowid.present) {
      map['rowid'] = Variable<int>(rowid.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('TopicTestsCompanion(')
          ..write('id: $id, ')
          ..write('studiedOn: $studiedOn, ')
          ..write('sourceId: $sourceId, ')
          ..write('sourceName: $sourceName, ')
          ..write('isRepeat: $isRepeat, ')
          ..write('difficulty: $difficulty, ')
          ..write('durationMinutes: $durationMinutes, ')
          ..write('note: $note, ')
          ..write('clientModifiedAt: $clientModifiedAt, ')
          ..write('deleted: $deleted, ')
          ..write('changeSeq: $changeSeq, ')
          ..write('rowid: $rowid')
          ..write(')'))
        .toString();
  }
}

class $TopicTestResultsTable extends TopicTestResults
    with TableInfo<$TopicTestResultsTable, TopicResultRow> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $TopicTestResultsTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _testIdMeta = const VerificationMeta('testId');
  @override
  late final GeneratedColumn<String> testId = GeneratedColumn<String>(
    'test_id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
    defaultConstraints: GeneratedColumn.constraintIsAlways(
      'REFERENCES topic_tests (id) ON DELETE CASCADE',
    ),
  );
  static const VerificationMeta _topicIdMeta = const VerificationMeta(
    'topicId',
  );
  @override
  late final GeneratedColumn<String> topicId = GeneratedColumn<String>(
    'topic_id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _totalMeta = const VerificationMeta('total');
  @override
  late final GeneratedColumn<int> total = GeneratedColumn<int>(
    'total',
    aliasedName,
    false,
    type: DriftSqlType.int,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _correctMeta = const VerificationMeta(
    'correct',
  );
  @override
  late final GeneratedColumn<int> correct = GeneratedColumn<int>(
    'correct',
    aliasedName,
    false,
    type: DriftSqlType.int,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _wrongMeta = const VerificationMeta('wrong');
  @override
  late final GeneratedColumn<int> wrong = GeneratedColumn<int>(
    'wrong',
    aliasedName,
    false,
    type: DriftSqlType.int,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _blankMeta = const VerificationMeta('blank');
  @override
  late final GeneratedColumn<int> blank = GeneratedColumn<int>(
    'blank',
    aliasedName,
    false,
    type: DriftSqlType.int,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _mistakeKnowledgeMeta = const VerificationMeta(
    'mistakeKnowledge',
  );
  @override
  late final GeneratedColumn<int> mistakeKnowledge = GeneratedColumn<int>(
    'mistake_knowledge',
    aliasedName,
    true,
    type: DriftSqlType.int,
    requiredDuringInsert: false,
  );
  static const VerificationMeta _mistakeAttentionMeta = const VerificationMeta(
    'mistakeAttention',
  );
  @override
  late final GeneratedColumn<int> mistakeAttention = GeneratedColumn<int>(
    'mistake_attention',
    aliasedName,
    true,
    type: DriftSqlType.int,
    requiredDuringInsert: false,
  );
  static const VerificationMeta _mistakeTimeMeta = const VerificationMeta(
    'mistakeTime',
  );
  @override
  late final GeneratedColumn<int> mistakeTime = GeneratedColumn<int>(
    'mistake_time',
    aliasedName,
    true,
    type: DriftSqlType.int,
    requiredDuringInsert: false,
  );
  @override
  List<GeneratedColumn> get $columns => [
    testId,
    topicId,
    total,
    correct,
    wrong,
    blank,
    mistakeKnowledge,
    mistakeAttention,
    mistakeTime,
  ];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'topic_test_results';
  @override
  VerificationContext validateIntegrity(
    Insertable<TopicResultRow> instance, {
    bool isInserting = false,
  }) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('test_id')) {
      context.handle(
        _testIdMeta,
        testId.isAcceptableOrUnknown(data['test_id']!, _testIdMeta),
      );
    } else if (isInserting) {
      context.missing(_testIdMeta);
    }
    if (data.containsKey('topic_id')) {
      context.handle(
        _topicIdMeta,
        topicId.isAcceptableOrUnknown(data['topic_id']!, _topicIdMeta),
      );
    } else if (isInserting) {
      context.missing(_topicIdMeta);
    }
    if (data.containsKey('total')) {
      context.handle(
        _totalMeta,
        total.isAcceptableOrUnknown(data['total']!, _totalMeta),
      );
    } else if (isInserting) {
      context.missing(_totalMeta);
    }
    if (data.containsKey('correct')) {
      context.handle(
        _correctMeta,
        correct.isAcceptableOrUnknown(data['correct']!, _correctMeta),
      );
    } else if (isInserting) {
      context.missing(_correctMeta);
    }
    if (data.containsKey('wrong')) {
      context.handle(
        _wrongMeta,
        wrong.isAcceptableOrUnknown(data['wrong']!, _wrongMeta),
      );
    } else if (isInserting) {
      context.missing(_wrongMeta);
    }
    if (data.containsKey('blank')) {
      context.handle(
        _blankMeta,
        blank.isAcceptableOrUnknown(data['blank']!, _blankMeta),
      );
    } else if (isInserting) {
      context.missing(_blankMeta);
    }
    if (data.containsKey('mistake_knowledge')) {
      context.handle(
        _mistakeKnowledgeMeta,
        mistakeKnowledge.isAcceptableOrUnknown(
          data['mistake_knowledge']!,
          _mistakeKnowledgeMeta,
        ),
      );
    }
    if (data.containsKey('mistake_attention')) {
      context.handle(
        _mistakeAttentionMeta,
        mistakeAttention.isAcceptableOrUnknown(
          data['mistake_attention']!,
          _mistakeAttentionMeta,
        ),
      );
    }
    if (data.containsKey('mistake_time')) {
      context.handle(
        _mistakeTimeMeta,
        mistakeTime.isAcceptableOrUnknown(
          data['mistake_time']!,
          _mistakeTimeMeta,
        ),
      );
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {testId, topicId};
  @override
  TopicResultRow map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return TopicResultRow(
      testId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}test_id'],
      )!,
      topicId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}topic_id'],
      )!,
      total: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}total'],
      )!,
      correct: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}correct'],
      )!,
      wrong: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}wrong'],
      )!,
      blank: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}blank'],
      )!,
      mistakeKnowledge: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}mistake_knowledge'],
      ),
      mistakeAttention: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}mistake_attention'],
      ),
      mistakeTime: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}mistake_time'],
      ),
    );
  }

  @override
  $TopicTestResultsTable createAlias(String alias) {
    return $TopicTestResultsTable(attachedDatabase, alias);
  }
}

class TopicResultRow extends DataClass implements Insertable<TopicResultRow> {
  final String testId;
  final String topicId;
  final int total;
  final int correct;
  final int wrong;
  final int blank;
  final int? mistakeKnowledge;
  final int? mistakeAttention;
  final int? mistakeTime;
  const TopicResultRow({
    required this.testId,
    required this.topicId,
    required this.total,
    required this.correct,
    required this.wrong,
    required this.blank,
    this.mistakeKnowledge,
    this.mistakeAttention,
    this.mistakeTime,
  });
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['test_id'] = Variable<String>(testId);
    map['topic_id'] = Variable<String>(topicId);
    map['total'] = Variable<int>(total);
    map['correct'] = Variable<int>(correct);
    map['wrong'] = Variable<int>(wrong);
    map['blank'] = Variable<int>(blank);
    if (!nullToAbsent || mistakeKnowledge != null) {
      map['mistake_knowledge'] = Variable<int>(mistakeKnowledge);
    }
    if (!nullToAbsent || mistakeAttention != null) {
      map['mistake_attention'] = Variable<int>(mistakeAttention);
    }
    if (!nullToAbsent || mistakeTime != null) {
      map['mistake_time'] = Variable<int>(mistakeTime);
    }
    return map;
  }

  TopicTestResultsCompanion toCompanion(bool nullToAbsent) {
    return TopicTestResultsCompanion(
      testId: Value(testId),
      topicId: Value(topicId),
      total: Value(total),
      correct: Value(correct),
      wrong: Value(wrong),
      blank: Value(blank),
      mistakeKnowledge: mistakeKnowledge == null && nullToAbsent
          ? const Value.absent()
          : Value(mistakeKnowledge),
      mistakeAttention: mistakeAttention == null && nullToAbsent
          ? const Value.absent()
          : Value(mistakeAttention),
      mistakeTime: mistakeTime == null && nullToAbsent
          ? const Value.absent()
          : Value(mistakeTime),
    );
  }

  factory TopicResultRow.fromJson(
    Map<String, dynamic> json, {
    ValueSerializer? serializer,
  }) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return TopicResultRow(
      testId: serializer.fromJson<String>(json['testId']),
      topicId: serializer.fromJson<String>(json['topicId']),
      total: serializer.fromJson<int>(json['total']),
      correct: serializer.fromJson<int>(json['correct']),
      wrong: serializer.fromJson<int>(json['wrong']),
      blank: serializer.fromJson<int>(json['blank']),
      mistakeKnowledge: serializer.fromJson<int?>(json['mistakeKnowledge']),
      mistakeAttention: serializer.fromJson<int?>(json['mistakeAttention']),
      mistakeTime: serializer.fromJson<int?>(json['mistakeTime']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'testId': serializer.toJson<String>(testId),
      'topicId': serializer.toJson<String>(topicId),
      'total': serializer.toJson<int>(total),
      'correct': serializer.toJson<int>(correct),
      'wrong': serializer.toJson<int>(wrong),
      'blank': serializer.toJson<int>(blank),
      'mistakeKnowledge': serializer.toJson<int?>(mistakeKnowledge),
      'mistakeAttention': serializer.toJson<int?>(mistakeAttention),
      'mistakeTime': serializer.toJson<int?>(mistakeTime),
    };
  }

  TopicResultRow copyWith({
    String? testId,
    String? topicId,
    int? total,
    int? correct,
    int? wrong,
    int? blank,
    Value<int?> mistakeKnowledge = const Value.absent(),
    Value<int?> mistakeAttention = const Value.absent(),
    Value<int?> mistakeTime = const Value.absent(),
  }) => TopicResultRow(
    testId: testId ?? this.testId,
    topicId: topicId ?? this.topicId,
    total: total ?? this.total,
    correct: correct ?? this.correct,
    wrong: wrong ?? this.wrong,
    blank: blank ?? this.blank,
    mistakeKnowledge: mistakeKnowledge.present
        ? mistakeKnowledge.value
        : this.mistakeKnowledge,
    mistakeAttention: mistakeAttention.present
        ? mistakeAttention.value
        : this.mistakeAttention,
    mistakeTime: mistakeTime.present ? mistakeTime.value : this.mistakeTime,
  );
  TopicResultRow copyWithCompanion(TopicTestResultsCompanion data) {
    return TopicResultRow(
      testId: data.testId.present ? data.testId.value : this.testId,
      topicId: data.topicId.present ? data.topicId.value : this.topicId,
      total: data.total.present ? data.total.value : this.total,
      correct: data.correct.present ? data.correct.value : this.correct,
      wrong: data.wrong.present ? data.wrong.value : this.wrong,
      blank: data.blank.present ? data.blank.value : this.blank,
      mistakeKnowledge: data.mistakeKnowledge.present
          ? data.mistakeKnowledge.value
          : this.mistakeKnowledge,
      mistakeAttention: data.mistakeAttention.present
          ? data.mistakeAttention.value
          : this.mistakeAttention,
      mistakeTime: data.mistakeTime.present
          ? data.mistakeTime.value
          : this.mistakeTime,
    );
  }

  @override
  String toString() {
    return (StringBuffer('TopicResultRow(')
          ..write('testId: $testId, ')
          ..write('topicId: $topicId, ')
          ..write('total: $total, ')
          ..write('correct: $correct, ')
          ..write('wrong: $wrong, ')
          ..write('blank: $blank, ')
          ..write('mistakeKnowledge: $mistakeKnowledge, ')
          ..write('mistakeAttention: $mistakeAttention, ')
          ..write('mistakeTime: $mistakeTime')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(
    testId,
    topicId,
    total,
    correct,
    wrong,
    blank,
    mistakeKnowledge,
    mistakeAttention,
    mistakeTime,
  );
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is TopicResultRow &&
          other.testId == this.testId &&
          other.topicId == this.topicId &&
          other.total == this.total &&
          other.correct == this.correct &&
          other.wrong == this.wrong &&
          other.blank == this.blank &&
          other.mistakeKnowledge == this.mistakeKnowledge &&
          other.mistakeAttention == this.mistakeAttention &&
          other.mistakeTime == this.mistakeTime);
}

class TopicTestResultsCompanion extends UpdateCompanion<TopicResultRow> {
  final Value<String> testId;
  final Value<String> topicId;
  final Value<int> total;
  final Value<int> correct;
  final Value<int> wrong;
  final Value<int> blank;
  final Value<int?> mistakeKnowledge;
  final Value<int?> mistakeAttention;
  final Value<int?> mistakeTime;
  final Value<int> rowid;
  const TopicTestResultsCompanion({
    this.testId = const Value.absent(),
    this.topicId = const Value.absent(),
    this.total = const Value.absent(),
    this.correct = const Value.absent(),
    this.wrong = const Value.absent(),
    this.blank = const Value.absent(),
    this.mistakeKnowledge = const Value.absent(),
    this.mistakeAttention = const Value.absent(),
    this.mistakeTime = const Value.absent(),
    this.rowid = const Value.absent(),
  });
  TopicTestResultsCompanion.insert({
    required String testId,
    required String topicId,
    required int total,
    required int correct,
    required int wrong,
    required int blank,
    this.mistakeKnowledge = const Value.absent(),
    this.mistakeAttention = const Value.absent(),
    this.mistakeTime = const Value.absent(),
    this.rowid = const Value.absent(),
  }) : testId = Value(testId),
       topicId = Value(topicId),
       total = Value(total),
       correct = Value(correct),
       wrong = Value(wrong),
       blank = Value(blank);
  static Insertable<TopicResultRow> custom({
    Expression<String>? testId,
    Expression<String>? topicId,
    Expression<int>? total,
    Expression<int>? correct,
    Expression<int>? wrong,
    Expression<int>? blank,
    Expression<int>? mistakeKnowledge,
    Expression<int>? mistakeAttention,
    Expression<int>? mistakeTime,
    Expression<int>? rowid,
  }) {
    return RawValuesInsertable({
      if (testId != null) 'test_id': testId,
      if (topicId != null) 'topic_id': topicId,
      if (total != null) 'total': total,
      if (correct != null) 'correct': correct,
      if (wrong != null) 'wrong': wrong,
      if (blank != null) 'blank': blank,
      if (mistakeKnowledge != null) 'mistake_knowledge': mistakeKnowledge,
      if (mistakeAttention != null) 'mistake_attention': mistakeAttention,
      if (mistakeTime != null) 'mistake_time': mistakeTime,
      if (rowid != null) 'rowid': rowid,
    });
  }

  TopicTestResultsCompanion copyWith({
    Value<String>? testId,
    Value<String>? topicId,
    Value<int>? total,
    Value<int>? correct,
    Value<int>? wrong,
    Value<int>? blank,
    Value<int?>? mistakeKnowledge,
    Value<int?>? mistakeAttention,
    Value<int?>? mistakeTime,
    Value<int>? rowid,
  }) {
    return TopicTestResultsCompanion(
      testId: testId ?? this.testId,
      topicId: topicId ?? this.topicId,
      total: total ?? this.total,
      correct: correct ?? this.correct,
      wrong: wrong ?? this.wrong,
      blank: blank ?? this.blank,
      mistakeKnowledge: mistakeKnowledge ?? this.mistakeKnowledge,
      mistakeAttention: mistakeAttention ?? this.mistakeAttention,
      mistakeTime: mistakeTime ?? this.mistakeTime,
      rowid: rowid ?? this.rowid,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (testId.present) {
      map['test_id'] = Variable<String>(testId.value);
    }
    if (topicId.present) {
      map['topic_id'] = Variable<String>(topicId.value);
    }
    if (total.present) {
      map['total'] = Variable<int>(total.value);
    }
    if (correct.present) {
      map['correct'] = Variable<int>(correct.value);
    }
    if (wrong.present) {
      map['wrong'] = Variable<int>(wrong.value);
    }
    if (blank.present) {
      map['blank'] = Variable<int>(blank.value);
    }
    if (mistakeKnowledge.present) {
      map['mistake_knowledge'] = Variable<int>(mistakeKnowledge.value);
    }
    if (mistakeAttention.present) {
      map['mistake_attention'] = Variable<int>(mistakeAttention.value);
    }
    if (mistakeTime.present) {
      map['mistake_time'] = Variable<int>(mistakeTime.value);
    }
    if (rowid.present) {
      map['rowid'] = Variable<int>(rowid.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('TopicTestResultsCompanion(')
          ..write('testId: $testId, ')
          ..write('topicId: $topicId, ')
          ..write('total: $total, ')
          ..write('correct: $correct, ')
          ..write('wrong: $wrong, ')
          ..write('blank: $blank, ')
          ..write('mistakeKnowledge: $mistakeKnowledge, ')
          ..write('mistakeAttention: $mistakeAttention, ')
          ..write('mistakeTime: $mistakeTime, ')
          ..write('rowid: $rowid')
          ..write(')'))
        .toString();
  }
}

class $OutboxTable extends Outbox with TableInfo<$OutboxTable, OutboxRow> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $OutboxTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _idMeta = const VerificationMeta('id');
  @override
  late final GeneratedColumn<int> id = GeneratedColumn<int>(
    'id',
    aliasedName,
    false,
    hasAutoIncrement: true,
    type: DriftSqlType.int,
    requiredDuringInsert: false,
    defaultConstraints: GeneratedColumn.constraintIsAlways(
      'PRIMARY KEY AUTOINCREMENT',
    ),
  );
  static const VerificationMeta _entityTypeMeta = const VerificationMeta(
    'entityType',
  );
  @override
  late final GeneratedColumn<String> entityType = GeneratedColumn<String>(
    'entity_type',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _entityIdMeta = const VerificationMeta(
    'entityId',
  );
  @override
  late final GeneratedColumn<String> entityId = GeneratedColumn<String>(
    'entity_id',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _operationMeta = const VerificationMeta(
    'operation',
  );
  @override
  late final GeneratedColumn<String> operation = GeneratedColumn<String>(
    'operation',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _payloadMeta = const VerificationMeta(
    'payload',
  );
  @override
  late final GeneratedColumn<String> payload = GeneratedColumn<String>(
    'payload',
    aliasedName,
    true,
    type: DriftSqlType.string,
    requiredDuringInsert: false,
  );
  static const VerificationMeta _revisionMeta = const VerificationMeta(
    'revision',
  );
  @override
  late final GeneratedColumn<int> revision = GeneratedColumn<int>(
    'revision',
    aliasedName,
    false,
    type: DriftSqlType.int,
    requiredDuringInsert: false,
    defaultValue: const Constant(0),
  );
  static const VerificationMeta _rejectedMeta = const VerificationMeta(
    'rejected',
  );
  @override
  late final GeneratedColumn<bool> rejected = GeneratedColumn<bool>(
    'rejected',
    aliasedName,
    false,
    type: DriftSqlType.bool,
    requiredDuringInsert: false,
    defaultConstraints: GeneratedColumn.constraintIsAlways(
      'CHECK ("rejected" IN (0, 1))',
    ),
    defaultValue: const Constant(false),
  );
  static const VerificationMeta _lastErrorMeta = const VerificationMeta(
    'lastError',
  );
  @override
  late final GeneratedColumn<String> lastError = GeneratedColumn<String>(
    'last_error',
    aliasedName,
    true,
    type: DriftSqlType.string,
    requiredDuringInsert: false,
  );
  @override
  List<GeneratedColumn> get $columns => [
    id,
    entityType,
    entityId,
    operation,
    payload,
    revision,
    rejected,
    lastError,
  ];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'outbox';
  @override
  VerificationContext validateIntegrity(
    Insertable<OutboxRow> instance, {
    bool isInserting = false,
  }) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('id')) {
      context.handle(_idMeta, id.isAcceptableOrUnknown(data['id']!, _idMeta));
    }
    if (data.containsKey('entity_type')) {
      context.handle(
        _entityTypeMeta,
        entityType.isAcceptableOrUnknown(data['entity_type']!, _entityTypeMeta),
      );
    } else if (isInserting) {
      context.missing(_entityTypeMeta);
    }
    if (data.containsKey('entity_id')) {
      context.handle(
        _entityIdMeta,
        entityId.isAcceptableOrUnknown(data['entity_id']!, _entityIdMeta),
      );
    } else if (isInserting) {
      context.missing(_entityIdMeta);
    }
    if (data.containsKey('operation')) {
      context.handle(
        _operationMeta,
        operation.isAcceptableOrUnknown(data['operation']!, _operationMeta),
      );
    } else if (isInserting) {
      context.missing(_operationMeta);
    }
    if (data.containsKey('payload')) {
      context.handle(
        _payloadMeta,
        payload.isAcceptableOrUnknown(data['payload']!, _payloadMeta),
      );
    }
    if (data.containsKey('revision')) {
      context.handle(
        _revisionMeta,
        revision.isAcceptableOrUnknown(data['revision']!, _revisionMeta),
      );
    }
    if (data.containsKey('rejected')) {
      context.handle(
        _rejectedMeta,
        rejected.isAcceptableOrUnknown(data['rejected']!, _rejectedMeta),
      );
    }
    if (data.containsKey('last_error')) {
      context.handle(
        _lastErrorMeta,
        lastError.isAcceptableOrUnknown(data['last_error']!, _lastErrorMeta),
      );
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {id};
  @override
  List<Set<GeneratedColumn>> get uniqueKeys => [
    {entityType, entityId},
  ];
  @override
  OutboxRow map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return OutboxRow(
      id: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}id'],
      )!,
      entityType: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}entity_type'],
      )!,
      entityId: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}entity_id'],
      )!,
      operation: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}operation'],
      )!,
      payload: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}payload'],
      ),
      revision: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}revision'],
      )!,
      rejected: attachedDatabase.typeMapping.read(
        DriftSqlType.bool,
        data['${effectivePrefix}rejected'],
      )!,
      lastError: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}last_error'],
      ),
    );
  }

  @override
  $OutboxTable createAlias(String alias) {
    return $OutboxTable(attachedDatabase, alias);
  }
}

class OutboxRow extends DataClass implements Insertable<OutboxRow> {
  final int id;
  final String entityType;
  final String entityId;

  /// `put` or `delete`.
  final String operation;
  final String? payload;

  /// Bumped when a newer write replaces the queued one, so an in-flight push cannot remove it.
  final int revision;

  /// The server refused it (400/404); it waits for the student to fix or drop the record.
  final bool rejected;
  final String? lastError;
  const OutboxRow({
    required this.id,
    required this.entityType,
    required this.entityId,
    required this.operation,
    this.payload,
    required this.revision,
    required this.rejected,
    this.lastError,
  });
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['id'] = Variable<int>(id);
    map['entity_type'] = Variable<String>(entityType);
    map['entity_id'] = Variable<String>(entityId);
    map['operation'] = Variable<String>(operation);
    if (!nullToAbsent || payload != null) {
      map['payload'] = Variable<String>(payload);
    }
    map['revision'] = Variable<int>(revision);
    map['rejected'] = Variable<bool>(rejected);
    if (!nullToAbsent || lastError != null) {
      map['last_error'] = Variable<String>(lastError);
    }
    return map;
  }

  OutboxCompanion toCompanion(bool nullToAbsent) {
    return OutboxCompanion(
      id: Value(id),
      entityType: Value(entityType),
      entityId: Value(entityId),
      operation: Value(operation),
      payload: payload == null && nullToAbsent
          ? const Value.absent()
          : Value(payload),
      revision: Value(revision),
      rejected: Value(rejected),
      lastError: lastError == null && nullToAbsent
          ? const Value.absent()
          : Value(lastError),
    );
  }

  factory OutboxRow.fromJson(
    Map<String, dynamic> json, {
    ValueSerializer? serializer,
  }) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return OutboxRow(
      id: serializer.fromJson<int>(json['id']),
      entityType: serializer.fromJson<String>(json['entityType']),
      entityId: serializer.fromJson<String>(json['entityId']),
      operation: serializer.fromJson<String>(json['operation']),
      payload: serializer.fromJson<String?>(json['payload']),
      revision: serializer.fromJson<int>(json['revision']),
      rejected: serializer.fromJson<bool>(json['rejected']),
      lastError: serializer.fromJson<String?>(json['lastError']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'id': serializer.toJson<int>(id),
      'entityType': serializer.toJson<String>(entityType),
      'entityId': serializer.toJson<String>(entityId),
      'operation': serializer.toJson<String>(operation),
      'payload': serializer.toJson<String?>(payload),
      'revision': serializer.toJson<int>(revision),
      'rejected': serializer.toJson<bool>(rejected),
      'lastError': serializer.toJson<String?>(lastError),
    };
  }

  OutboxRow copyWith({
    int? id,
    String? entityType,
    String? entityId,
    String? operation,
    Value<String?> payload = const Value.absent(),
    int? revision,
    bool? rejected,
    Value<String?> lastError = const Value.absent(),
  }) => OutboxRow(
    id: id ?? this.id,
    entityType: entityType ?? this.entityType,
    entityId: entityId ?? this.entityId,
    operation: operation ?? this.operation,
    payload: payload.present ? payload.value : this.payload,
    revision: revision ?? this.revision,
    rejected: rejected ?? this.rejected,
    lastError: lastError.present ? lastError.value : this.lastError,
  );
  OutboxRow copyWithCompanion(OutboxCompanion data) {
    return OutboxRow(
      id: data.id.present ? data.id.value : this.id,
      entityType: data.entityType.present
          ? data.entityType.value
          : this.entityType,
      entityId: data.entityId.present ? data.entityId.value : this.entityId,
      operation: data.operation.present ? data.operation.value : this.operation,
      payload: data.payload.present ? data.payload.value : this.payload,
      revision: data.revision.present ? data.revision.value : this.revision,
      rejected: data.rejected.present ? data.rejected.value : this.rejected,
      lastError: data.lastError.present ? data.lastError.value : this.lastError,
    );
  }

  @override
  String toString() {
    return (StringBuffer('OutboxRow(')
          ..write('id: $id, ')
          ..write('entityType: $entityType, ')
          ..write('entityId: $entityId, ')
          ..write('operation: $operation, ')
          ..write('payload: $payload, ')
          ..write('revision: $revision, ')
          ..write('rejected: $rejected, ')
          ..write('lastError: $lastError')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(
    id,
    entityType,
    entityId,
    operation,
    payload,
    revision,
    rejected,
    lastError,
  );
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is OutboxRow &&
          other.id == this.id &&
          other.entityType == this.entityType &&
          other.entityId == this.entityId &&
          other.operation == this.operation &&
          other.payload == this.payload &&
          other.revision == this.revision &&
          other.rejected == this.rejected &&
          other.lastError == this.lastError);
}

class OutboxCompanion extends UpdateCompanion<OutboxRow> {
  final Value<int> id;
  final Value<String> entityType;
  final Value<String> entityId;
  final Value<String> operation;
  final Value<String?> payload;
  final Value<int> revision;
  final Value<bool> rejected;
  final Value<String?> lastError;
  const OutboxCompanion({
    this.id = const Value.absent(),
    this.entityType = const Value.absent(),
    this.entityId = const Value.absent(),
    this.operation = const Value.absent(),
    this.payload = const Value.absent(),
    this.revision = const Value.absent(),
    this.rejected = const Value.absent(),
    this.lastError = const Value.absent(),
  });
  OutboxCompanion.insert({
    this.id = const Value.absent(),
    required String entityType,
    required String entityId,
    required String operation,
    this.payload = const Value.absent(),
    this.revision = const Value.absent(),
    this.rejected = const Value.absent(),
    this.lastError = const Value.absent(),
  }) : entityType = Value(entityType),
       entityId = Value(entityId),
       operation = Value(operation);
  static Insertable<OutboxRow> custom({
    Expression<int>? id,
    Expression<String>? entityType,
    Expression<String>? entityId,
    Expression<String>? operation,
    Expression<String>? payload,
    Expression<int>? revision,
    Expression<bool>? rejected,
    Expression<String>? lastError,
  }) {
    return RawValuesInsertable({
      if (id != null) 'id': id,
      if (entityType != null) 'entity_type': entityType,
      if (entityId != null) 'entity_id': entityId,
      if (operation != null) 'operation': operation,
      if (payload != null) 'payload': payload,
      if (revision != null) 'revision': revision,
      if (rejected != null) 'rejected': rejected,
      if (lastError != null) 'last_error': lastError,
    });
  }

  OutboxCompanion copyWith({
    Value<int>? id,
    Value<String>? entityType,
    Value<String>? entityId,
    Value<String>? operation,
    Value<String?>? payload,
    Value<int>? revision,
    Value<bool>? rejected,
    Value<String?>? lastError,
  }) {
    return OutboxCompanion(
      id: id ?? this.id,
      entityType: entityType ?? this.entityType,
      entityId: entityId ?? this.entityId,
      operation: operation ?? this.operation,
      payload: payload ?? this.payload,
      revision: revision ?? this.revision,
      rejected: rejected ?? this.rejected,
      lastError: lastError ?? this.lastError,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (id.present) {
      map['id'] = Variable<int>(id.value);
    }
    if (entityType.present) {
      map['entity_type'] = Variable<String>(entityType.value);
    }
    if (entityId.present) {
      map['entity_id'] = Variable<String>(entityId.value);
    }
    if (operation.present) {
      map['operation'] = Variable<String>(operation.value);
    }
    if (payload.present) {
      map['payload'] = Variable<String>(payload.value);
    }
    if (revision.present) {
      map['revision'] = Variable<int>(revision.value);
    }
    if (rejected.present) {
      map['rejected'] = Variable<bool>(rejected.value);
    }
    if (lastError.present) {
      map['last_error'] = Variable<String>(lastError.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('OutboxCompanion(')
          ..write('id: $id, ')
          ..write('entityType: $entityType, ')
          ..write('entityId: $entityId, ')
          ..write('operation: $operation, ')
          ..write('payload: $payload, ')
          ..write('revision: $revision, ')
          ..write('rejected: $rejected, ')
          ..write('lastError: $lastError')
          ..write(')'))
        .toString();
  }
}

class $SyncCursorsTable extends SyncCursors
    with TableInfo<$SyncCursorsTable, SyncCursorRow> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $SyncCursorsTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _streamMeta = const VerificationMeta('stream');
  @override
  late final GeneratedColumn<String> stream = GeneratedColumn<String>(
    'stream',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _cursorMeta = const VerificationMeta('cursor');
  @override
  late final GeneratedColumn<int> cursor = GeneratedColumn<int>(
    'cursor',
    aliasedName,
    false,
    type: DriftSqlType.int,
    requiredDuringInsert: true,
  );
  @override
  List<GeneratedColumn> get $columns => [stream, cursor];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'sync_cursors';
  @override
  VerificationContext validateIntegrity(
    Insertable<SyncCursorRow> instance, {
    bool isInserting = false,
  }) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('stream')) {
      context.handle(
        _streamMeta,
        stream.isAcceptableOrUnknown(data['stream']!, _streamMeta),
      );
    } else if (isInserting) {
      context.missing(_streamMeta);
    }
    if (data.containsKey('cursor')) {
      context.handle(
        _cursorMeta,
        cursor.isAcceptableOrUnknown(data['cursor']!, _cursorMeta),
      );
    } else if (isInserting) {
      context.missing(_cursorMeta);
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {stream};
  @override
  SyncCursorRow map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return SyncCursorRow(
      stream: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}stream'],
      )!,
      cursor: attachedDatabase.typeMapping.read(
        DriftSqlType.int,
        data['${effectivePrefix}cursor'],
      )!,
    );
  }

  @override
  $SyncCursorsTable createAlias(String alias) {
    return $SyncCursorsTable(attachedDatabase, alias);
  }
}

class SyncCursorRow extends DataClass implements Insertable<SyncCursorRow> {
  final String stream;
  final int cursor;
  const SyncCursorRow({required this.stream, required this.cursor});
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['stream'] = Variable<String>(stream);
    map['cursor'] = Variable<int>(cursor);
    return map;
  }

  SyncCursorsCompanion toCompanion(bool nullToAbsent) {
    return SyncCursorsCompanion(stream: Value(stream), cursor: Value(cursor));
  }

  factory SyncCursorRow.fromJson(
    Map<String, dynamic> json, {
    ValueSerializer? serializer,
  }) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return SyncCursorRow(
      stream: serializer.fromJson<String>(json['stream']),
      cursor: serializer.fromJson<int>(json['cursor']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'stream': serializer.toJson<String>(stream),
      'cursor': serializer.toJson<int>(cursor),
    };
  }

  SyncCursorRow copyWith({String? stream, int? cursor}) => SyncCursorRow(
    stream: stream ?? this.stream,
    cursor: cursor ?? this.cursor,
  );
  SyncCursorRow copyWithCompanion(SyncCursorsCompanion data) {
    return SyncCursorRow(
      stream: data.stream.present ? data.stream.value : this.stream,
      cursor: data.cursor.present ? data.cursor.value : this.cursor,
    );
  }

  @override
  String toString() {
    return (StringBuffer('SyncCursorRow(')
          ..write('stream: $stream, ')
          ..write('cursor: $cursor')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(stream, cursor);
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is SyncCursorRow &&
          other.stream == this.stream &&
          other.cursor == this.cursor);
}

class SyncCursorsCompanion extends UpdateCompanion<SyncCursorRow> {
  final Value<String> stream;
  final Value<int> cursor;
  final Value<int> rowid;
  const SyncCursorsCompanion({
    this.stream = const Value.absent(),
    this.cursor = const Value.absent(),
    this.rowid = const Value.absent(),
  });
  SyncCursorsCompanion.insert({
    required String stream,
    required int cursor,
    this.rowid = const Value.absent(),
  }) : stream = Value(stream),
       cursor = Value(cursor);
  static Insertable<SyncCursorRow> custom({
    Expression<String>? stream,
    Expression<int>? cursor,
    Expression<int>? rowid,
  }) {
    return RawValuesInsertable({
      if (stream != null) 'stream': stream,
      if (cursor != null) 'cursor': cursor,
      if (rowid != null) 'rowid': rowid,
    });
  }

  SyncCursorsCompanion copyWith({
    Value<String>? stream,
    Value<int>? cursor,
    Value<int>? rowid,
  }) {
    return SyncCursorsCompanion(
      stream: stream ?? this.stream,
      cursor: cursor ?? this.cursor,
      rowid: rowid ?? this.rowid,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (stream.present) {
      map['stream'] = Variable<String>(stream.value);
    }
    if (cursor.present) {
      map['cursor'] = Variable<int>(cursor.value);
    }
    if (rowid.present) {
      map['rowid'] = Variable<int>(rowid.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('SyncCursorsCompanion(')
          ..write('stream: $stream, ')
          ..write('cursor: $cursor, ')
          ..write('rowid: $rowid')
          ..write(')'))
        .toString();
  }
}

class $CacheEntriesTable extends CacheEntries
    with TableInfo<$CacheEntriesTable, CacheEntryRow> {
  @override
  final GeneratedDatabase attachedDatabase;
  final String? _alias;
  $CacheEntriesTable(this.attachedDatabase, [this._alias]);
  static const VerificationMeta _keyMeta = const VerificationMeta('key');
  @override
  late final GeneratedColumn<String> key = GeneratedColumn<String>(
    'key',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  static const VerificationMeta _valueMeta = const VerificationMeta('value');
  @override
  late final GeneratedColumn<String> value = GeneratedColumn<String>(
    'value',
    aliasedName,
    false,
    type: DriftSqlType.string,
    requiredDuringInsert: true,
  );
  @override
  List<GeneratedColumn> get $columns => [key, value];
  @override
  String get aliasedName => _alias ?? actualTableName;
  @override
  String get actualTableName => $name;
  static const String $name = 'cache_entries';
  @override
  VerificationContext validateIntegrity(
    Insertable<CacheEntryRow> instance, {
    bool isInserting = false,
  }) {
    final context = VerificationContext();
    final data = instance.toColumns(true);
    if (data.containsKey('key')) {
      context.handle(
        _keyMeta,
        key.isAcceptableOrUnknown(data['key']!, _keyMeta),
      );
    } else if (isInserting) {
      context.missing(_keyMeta);
    }
    if (data.containsKey('value')) {
      context.handle(
        _valueMeta,
        value.isAcceptableOrUnknown(data['value']!, _valueMeta),
      );
    } else if (isInserting) {
      context.missing(_valueMeta);
    }
    return context;
  }

  @override
  Set<GeneratedColumn> get $primaryKey => {key};
  @override
  CacheEntryRow map(Map<String, dynamic> data, {String? tablePrefix}) {
    final effectivePrefix = tablePrefix != null ? '$tablePrefix.' : '';
    return CacheEntryRow(
      key: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}key'],
      )!,
      value: attachedDatabase.typeMapping.read(
        DriftSqlType.string,
        data['${effectivePrefix}value'],
      )!,
    );
  }

  @override
  $CacheEntriesTable createAlias(String alias) {
    return $CacheEntriesTable(attachedDatabase, alias);
  }
}

class CacheEntryRow extends DataClass implements Insertable<CacheEntryRow> {
  final String key;
  final String value;
  const CacheEntryRow({required this.key, required this.value});
  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    map['key'] = Variable<String>(key);
    map['value'] = Variable<String>(value);
    return map;
  }

  CacheEntriesCompanion toCompanion(bool nullToAbsent) {
    return CacheEntriesCompanion(key: Value(key), value: Value(value));
  }

  factory CacheEntryRow.fromJson(
    Map<String, dynamic> json, {
    ValueSerializer? serializer,
  }) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return CacheEntryRow(
      key: serializer.fromJson<String>(json['key']),
      value: serializer.fromJson<String>(json['value']),
    );
  }
  @override
  Map<String, dynamic> toJson({ValueSerializer? serializer}) {
    serializer ??= driftRuntimeOptions.defaultSerializer;
    return <String, dynamic>{
      'key': serializer.toJson<String>(key),
      'value': serializer.toJson<String>(value),
    };
  }

  CacheEntryRow copyWith({String? key, String? value}) =>
      CacheEntryRow(key: key ?? this.key, value: value ?? this.value);
  CacheEntryRow copyWithCompanion(CacheEntriesCompanion data) {
    return CacheEntryRow(
      key: data.key.present ? data.key.value : this.key,
      value: data.value.present ? data.value.value : this.value,
    );
  }

  @override
  String toString() {
    return (StringBuffer('CacheEntryRow(')
          ..write('key: $key, ')
          ..write('value: $value')
          ..write(')'))
        .toString();
  }

  @override
  int get hashCode => Object.hash(key, value);
  @override
  bool operator ==(Object other) =>
      identical(this, other) ||
      (other is CacheEntryRow &&
          other.key == this.key &&
          other.value == this.value);
}

class CacheEntriesCompanion extends UpdateCompanion<CacheEntryRow> {
  final Value<String> key;
  final Value<String> value;
  final Value<int> rowid;
  const CacheEntriesCompanion({
    this.key = const Value.absent(),
    this.value = const Value.absent(),
    this.rowid = const Value.absent(),
  });
  CacheEntriesCompanion.insert({
    required String key,
    required String value,
    this.rowid = const Value.absent(),
  }) : key = Value(key),
       value = Value(value);
  static Insertable<CacheEntryRow> custom({
    Expression<String>? key,
    Expression<String>? value,
    Expression<int>? rowid,
  }) {
    return RawValuesInsertable({
      if (key != null) 'key': key,
      if (value != null) 'value': value,
      if (rowid != null) 'rowid': rowid,
    });
  }

  CacheEntriesCompanion copyWith({
    Value<String>? key,
    Value<String>? value,
    Value<int>? rowid,
  }) {
    return CacheEntriesCompanion(
      key: key ?? this.key,
      value: value ?? this.value,
      rowid: rowid ?? this.rowid,
    );
  }

  @override
  Map<String, Expression> toColumns(bool nullToAbsent) {
    final map = <String, Expression>{};
    if (key.present) {
      map['key'] = Variable<String>(key.value);
    }
    if (value.present) {
      map['value'] = Variable<String>(value.value);
    }
    if (rowid.present) {
      map['rowid'] = Variable<int>(rowid.value);
    }
    return map;
  }

  @override
  String toString() {
    return (StringBuffer('CacheEntriesCompanion(')
          ..write('key: $key, ')
          ..write('value: $value, ')
          ..write('rowid: $rowid')
          ..write(')'))
        .toString();
  }
}

abstract class _$AppDatabase extends GeneratedDatabase {
  _$AppDatabase(QueryExecutor e) : super(e);
  $AppDatabaseManager get managers => $AppDatabaseManager(this);
  late final $TopicTestsTable topicTests = $TopicTestsTable(this);
  late final $TopicTestResultsTable topicTestResults = $TopicTestResultsTable(
    this,
  );
  late final $OutboxTable outbox = $OutboxTable(this);
  late final $SyncCursorsTable syncCursors = $SyncCursorsTable(this);
  late final $CacheEntriesTable cacheEntries = $CacheEntriesTable(this);
  @override
  Iterable<TableInfo<Table, Object?>> get allTables =>
      allSchemaEntities.whereType<TableInfo<Table, Object?>>();
  @override
  List<DatabaseSchemaEntity> get allSchemaEntities => [
    topicTests,
    topicTestResults,
    outbox,
    syncCursors,
    cacheEntries,
  ];
  @override
  StreamQueryUpdateRules get streamUpdateRules => const StreamQueryUpdateRules([
    WritePropagation(
      on: TableUpdateQuery.onTableName(
        'topic_tests',
        limitUpdateKind: UpdateKind.delete,
      ),
      result: [TableUpdate('topic_test_results', kind: UpdateKind.delete)],
    ),
  ]);
}

typedef $$TopicTestsTableCreateCompanionBuilder = TopicTestsCompanion Function({
  required String id,
  required String studiedOn,
  Value<String?> sourceId,
  Value<String?> sourceName,
  required bool isRepeat,
  Value<String?> difficulty,
  Value<int?> durationMinutes,
  Value<String?> note,
  required int clientModifiedAt,
  Value<bool> deleted,
  Value<int> changeSeq,
  Value<int> rowid,
});
typedef $$TopicTestsTableUpdateCompanionBuilder = TopicTestsCompanion Function({
  Value<String> id,
  Value<String> studiedOn,
  Value<String?> sourceId,
  Value<String?> sourceName,
  Value<bool> isRepeat,
  Value<String?> difficulty,
  Value<int?> durationMinutes,
  Value<String?> note,
  Value<int> clientModifiedAt,
  Value<bool> deleted,
  Value<int> changeSeq,
  Value<int> rowid,
});

final class $$TopicTestsTableReferences
    extends BaseReferences<_$AppDatabase, $TopicTestsTable, TopicTestRow> {
  $$TopicTestsTableReferences(super.$_db, super.$_table, super.$_typedResult);

  static MultiTypedResultKey<$TopicTestResultsTable, List<TopicResultRow>>
  _topicTestResultsRefsTable(_$AppDatabase db) => MultiTypedResultKey.fromTable(
    db.topicTestResults,
    aliasName: 'topic_tests__id__topic_test_results__test_id',
  );

  $$TopicTestResultsTableProcessedTableManager get topicTestResultsRefs {
    final manager = $$TopicTestResultsTableTableManager(
      $_db,
      $_db.topicTestResults,
    ).filter((f) => f.testId.id.sqlEquals($_itemColumn<String>('id')!));

    final cache = $_typedResult.readTableOrNull(
      _topicTestResultsRefsTable($_db),
    );
    return ProcessedTableManager(
      manager.$state.copyWith(prefetchedData: cache),
    );
  }
}

class $$TopicTestsTableFilterComposer
    extends Composer<_$AppDatabase, $TopicTestsTable> {
  $$TopicTestsTableFilterComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnFilters<String> get id => $composableBuilder(
    column: $table.id,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get studiedOn => $composableBuilder(
    column: $table.studiedOn,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get sourceId => $composableBuilder(
    column: $table.sourceId,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get sourceName => $composableBuilder(
    column: $table.sourceName,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<bool> get isRepeat => $composableBuilder(
    column: $table.isRepeat,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get difficulty => $composableBuilder(
    column: $table.difficulty,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get durationMinutes => $composableBuilder(
    column: $table.durationMinutes,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get note => $composableBuilder(
    column: $table.note,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get clientModifiedAt => $composableBuilder(
    column: $table.clientModifiedAt,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<bool> get deleted => $composableBuilder(
    column: $table.deleted,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get changeSeq => $composableBuilder(
    column: $table.changeSeq,
    builder: (column) => ColumnFilters(column),
  );

  Expression<bool> topicTestResultsRefs(
    Expression<bool> Function($$TopicTestResultsTableFilterComposer f) f,
  ) {
    final $$TopicTestResultsTableFilterComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.id,
      referencedTable: $db.topicTestResults,
      getReferencedColumn: (t) => t.testId,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$TopicTestResultsTableFilterComposer(
            $db: $db,
            $table: $db.topicTestResults,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return f(composer);
  }
}

class $$TopicTestsTableOrderingComposer
    extends Composer<_$AppDatabase, $TopicTestsTable> {
  $$TopicTestsTableOrderingComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnOrderings<String> get id => $composableBuilder(
    column: $table.id,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get studiedOn => $composableBuilder(
    column: $table.studiedOn,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get sourceId => $composableBuilder(
    column: $table.sourceId,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get sourceName => $composableBuilder(
    column: $table.sourceName,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<bool> get isRepeat => $composableBuilder(
    column: $table.isRepeat,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get difficulty => $composableBuilder(
    column: $table.difficulty,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get durationMinutes => $composableBuilder(
    column: $table.durationMinutes,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get note => $composableBuilder(
    column: $table.note,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get clientModifiedAt => $composableBuilder(
    column: $table.clientModifiedAt,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<bool> get deleted => $composableBuilder(
    column: $table.deleted,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get changeSeq => $composableBuilder(
    column: $table.changeSeq,
    builder: (column) => ColumnOrderings(column),
  );
}

class $$TopicTestsTableAnnotationComposer
    extends Composer<_$AppDatabase, $TopicTestsTable> {
  $$TopicTestsTableAnnotationComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  GeneratedColumn<String> get id =>
      $composableBuilder(column: $table.id, builder: (column) => column);

  GeneratedColumn<String> get studiedOn =>
      $composableBuilder(column: $table.studiedOn, builder: (column) => column);

  GeneratedColumn<String> get sourceId =>
      $composableBuilder(column: $table.sourceId, builder: (column) => column);

  GeneratedColumn<String> get sourceName => $composableBuilder(
    column: $table.sourceName,
    builder: (column) => column,
  );

  GeneratedColumn<bool> get isRepeat =>
      $composableBuilder(column: $table.isRepeat, builder: (column) => column);

  GeneratedColumn<String> get difficulty => $composableBuilder(
    column: $table.difficulty,
    builder: (column) => column,
  );

  GeneratedColumn<int> get durationMinutes => $composableBuilder(
    column: $table.durationMinutes,
    builder: (column) => column,
  );

  GeneratedColumn<String> get note =>
      $composableBuilder(column: $table.note, builder: (column) => column);

  GeneratedColumn<int> get clientModifiedAt => $composableBuilder(
    column: $table.clientModifiedAt,
    builder: (column) => column,
  );

  GeneratedColumn<bool> get deleted =>
      $composableBuilder(column: $table.deleted, builder: (column) => column);

  GeneratedColumn<int> get changeSeq =>
      $composableBuilder(column: $table.changeSeq, builder: (column) => column);

  Expression<T> topicTestResultsRefs<T extends Object>(
    Expression<T> Function($$TopicTestResultsTableAnnotationComposer a) f,
  ) {
    final $$TopicTestResultsTableAnnotationComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.id,
      referencedTable: $db.topicTestResults,
      getReferencedColumn: (t) => t.testId,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$TopicTestResultsTableAnnotationComposer(
            $db: $db,
            $table: $db.topicTestResults,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return f(composer);
  }
}

class $$TopicTestsTableTableManager
    extends
        RootTableManager<
          _$AppDatabase,
          $TopicTestsTable,
          TopicTestRow,
          $$TopicTestsTableFilterComposer,
          $$TopicTestsTableOrderingComposer,
          $$TopicTestsTableAnnotationComposer,
          $$TopicTestsTableCreateCompanionBuilder,
          $$TopicTestsTableUpdateCompanionBuilder,
          (TopicTestRow, $$TopicTestsTableReferences),
          TopicTestRow,
          PrefetchHooks Function({bool topicTestResultsRefs})
        > {
  $$TopicTestsTableTableManager(_$AppDatabase db, $TopicTestsTable table)
    : super(
        TableManagerState(
          db: db,
          table: table,
          createFilteringComposer: () =>
              $$TopicTestsTableFilterComposer($db: db, $table: table),
          createOrderingComposer: () =>
              $$TopicTestsTableOrderingComposer($db: db, $table: table),
          createComputedFieldComposer: () =>
              $$TopicTestsTableAnnotationComposer($db: db, $table: table),
          updateCompanionCallback:
              ({
                Value<String> id = const Value.absent(),
                Value<String> studiedOn = const Value.absent(),
                Value<String?> sourceId = const Value.absent(),
                Value<String?> sourceName = const Value.absent(),
                Value<bool> isRepeat = const Value.absent(),
                Value<String?> difficulty = const Value.absent(),
                Value<int?> durationMinutes = const Value.absent(),
                Value<String?> note = const Value.absent(),
                Value<int> clientModifiedAt = const Value.absent(),
                Value<bool> deleted = const Value.absent(),
                Value<int> changeSeq = const Value.absent(),
                Value<int> rowid = const Value.absent(),
              }) => TopicTestsCompanion(
                id: id,
                studiedOn: studiedOn,
                sourceId: sourceId,
                sourceName: sourceName,
                isRepeat: isRepeat,
                difficulty: difficulty,
                durationMinutes: durationMinutes,
                note: note,
                clientModifiedAt: clientModifiedAt,
                deleted: deleted,
                changeSeq: changeSeq,
                rowid: rowid,
              ),
          createCompanionCallback:
              ({
                required String id,
                required String studiedOn,
                Value<String?> sourceId = const Value.absent(),
                Value<String?> sourceName = const Value.absent(),
                required bool isRepeat,
                Value<String?> difficulty = const Value.absent(),
                Value<int?> durationMinutes = const Value.absent(),
                Value<String?> note = const Value.absent(),
                required int clientModifiedAt,
                Value<bool> deleted = const Value.absent(),
                Value<int> changeSeq = const Value.absent(),
                Value<int> rowid = const Value.absent(),
              }) => TopicTestsCompanion.insert(
                id: id,
                studiedOn: studiedOn,
                sourceId: sourceId,
                sourceName: sourceName,
                isRepeat: isRepeat,
                difficulty: difficulty,
                durationMinutes: durationMinutes,
                note: note,
                clientModifiedAt: clientModifiedAt,
                deleted: deleted,
                changeSeq: changeSeq,
                rowid: rowid,
              ),
          withReferenceMapper: (p0) => p0
              .map(
                (e) => (
                  e.readTable<$TopicTestsTable, TopicTestRow>(table),
                  $$TopicTestsTableReferences(db, table, e),
                ),
              )
              .toList(),
          prefetchHooksCallback: ({topicTestResultsRefs = false}) {
            return PrefetchHooks(
              db: db,
              explicitlyWatchedTables: [
                if (topicTestResultsRefs) db.topicTestResults,
              ],
              addJoins: null,
              getPrefetchedDataCallback: (items) async {
                return [
                  if (topicTestResultsRefs)
                    await $_getPrefetchedDataByColumns<
                      TopicTestRow,
                      $TopicTestsTable,
                      TopicResultRow,
                      $TopicTestResultsTable
                    >(
                      currentTable: table,
                      referencedTable: $$TopicTestsTableReferences
                          ._topicTestResultsRefsTable(db),
                      managerFromTypedResult: (p0) =>
                          $$TopicTestsTableReferences(
                            db,
                            table,
                            p0,
                          ).topicTestResultsRefs,
                      currentColumn: 'id',
                      referencedColumn: 'test_id',
                      typedResults: items,
                    ),
                ];
              },
            );
          },
        ),
      );
}

typedef $$TopicTestsTableProcessedTableManager =
    ProcessedTableManager<
      _$AppDatabase,
      $TopicTestsTable,
      TopicTestRow,
      $$TopicTestsTableFilterComposer,
      $$TopicTestsTableOrderingComposer,
      $$TopicTestsTableAnnotationComposer,
      $$TopicTestsTableCreateCompanionBuilder,
      $$TopicTestsTableUpdateCompanionBuilder,
      (TopicTestRow, $$TopicTestsTableReferences),
      TopicTestRow,
      PrefetchHooks Function({bool topicTestResultsRefs})
    >;
typedef $$TopicTestResultsTableCreateCompanionBuilder =
    TopicTestResultsCompanion Function({
      required String testId,
      required String topicId,
      required int total,
      required int correct,
      required int wrong,
      required int blank,
      Value<int?> mistakeKnowledge,
      Value<int?> mistakeAttention,
      Value<int?> mistakeTime,
      Value<int> rowid,
    });
typedef $$TopicTestResultsTableUpdateCompanionBuilder =
    TopicTestResultsCompanion Function({
      Value<String> testId,
      Value<String> topicId,
      Value<int> total,
      Value<int> correct,
      Value<int> wrong,
      Value<int> blank,
      Value<int?> mistakeKnowledge,
      Value<int?> mistakeAttention,
      Value<int?> mistakeTime,
      Value<int> rowid,
    });

final class $$TopicTestResultsTableReferences
    extends
        BaseReferences<_$AppDatabase, $TopicTestResultsTable, TopicResultRow> {
  $$TopicTestResultsTableReferences(
    super.$_db,
    super.$_table,
    super.$_typedResult,
  );

  static $TopicTestsTable _testIdTable(_$AppDatabase db) =>
      db.topicTests.createAlias('topic_test_results__test_id__topic_tests__id');

  $$TopicTestsTableProcessedTableManager get testId {
    final $_column = $_itemColumn<String>('test_id')!;

    final manager = $$TopicTestsTableTableManager(
      $_db,
      $_db.topicTests,
    ).filter((f) => f.id.sqlEquals($_column));
    final item = $_typedResult.readTableOrNull(_testIdTable($_db));
    if (item == null) return manager;
    return ProcessedTableManager(
      manager.$state.copyWith(prefetchedData: [item]),
    );
  }
}

class $$TopicTestResultsTableFilterComposer
    extends Composer<_$AppDatabase, $TopicTestResultsTable> {
  $$TopicTestResultsTableFilterComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnFilters<String> get topicId => $composableBuilder(
    column: $table.topicId,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get total => $composableBuilder(
    column: $table.total,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get correct => $composableBuilder(
    column: $table.correct,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get wrong => $composableBuilder(
    column: $table.wrong,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get blank => $composableBuilder(
    column: $table.blank,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get mistakeKnowledge => $composableBuilder(
    column: $table.mistakeKnowledge,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get mistakeAttention => $composableBuilder(
    column: $table.mistakeAttention,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get mistakeTime => $composableBuilder(
    column: $table.mistakeTime,
    builder: (column) => ColumnFilters(column),
  );

  $$TopicTestsTableFilterComposer get testId {
    final $$TopicTestsTableFilterComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.testId,
      referencedTable: $db.topicTests,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$TopicTestsTableFilterComposer(
            $db: $db,
            $table: $db.topicTests,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }
}

class $$TopicTestResultsTableOrderingComposer
    extends Composer<_$AppDatabase, $TopicTestResultsTable> {
  $$TopicTestResultsTableOrderingComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnOrderings<String> get topicId => $composableBuilder(
    column: $table.topicId,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get total => $composableBuilder(
    column: $table.total,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get correct => $composableBuilder(
    column: $table.correct,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get wrong => $composableBuilder(
    column: $table.wrong,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get blank => $composableBuilder(
    column: $table.blank,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get mistakeKnowledge => $composableBuilder(
    column: $table.mistakeKnowledge,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get mistakeAttention => $composableBuilder(
    column: $table.mistakeAttention,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get mistakeTime => $composableBuilder(
    column: $table.mistakeTime,
    builder: (column) => ColumnOrderings(column),
  );

  $$TopicTestsTableOrderingComposer get testId {
    final $$TopicTestsTableOrderingComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.testId,
      referencedTable: $db.topicTests,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$TopicTestsTableOrderingComposer(
            $db: $db,
            $table: $db.topicTests,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }
}

class $$TopicTestResultsTableAnnotationComposer
    extends Composer<_$AppDatabase, $TopicTestResultsTable> {
  $$TopicTestResultsTableAnnotationComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  GeneratedColumn<String> get topicId =>
      $composableBuilder(column: $table.topicId, builder: (column) => column);

  GeneratedColumn<int> get total =>
      $composableBuilder(column: $table.total, builder: (column) => column);

  GeneratedColumn<int> get correct =>
      $composableBuilder(column: $table.correct, builder: (column) => column);

  GeneratedColumn<int> get wrong =>
      $composableBuilder(column: $table.wrong, builder: (column) => column);

  GeneratedColumn<int> get blank =>
      $composableBuilder(column: $table.blank, builder: (column) => column);

  GeneratedColumn<int> get mistakeKnowledge => $composableBuilder(
    column: $table.mistakeKnowledge,
    builder: (column) => column,
  );

  GeneratedColumn<int> get mistakeAttention => $composableBuilder(
    column: $table.mistakeAttention,
    builder: (column) => column,
  );

  GeneratedColumn<int> get mistakeTime => $composableBuilder(
    column: $table.mistakeTime,
    builder: (column) => column,
  );

  $$TopicTestsTableAnnotationComposer get testId {
    final $$TopicTestsTableAnnotationComposer composer = $composerBuilder(
      composer: this,
      getCurrentColumn: (t) => t.testId,
      referencedTable: $db.topicTests,
      getReferencedColumn: (t) => t.id,
      builder:
          (
            joinBuilder, {
            $addJoinBuilderToRootComposer,
            $removeJoinBuilderFromRootComposer,
          }) => $$TopicTestsTableAnnotationComposer(
            $db: $db,
            $table: $db.topicTests,
            $addJoinBuilderToRootComposer: $addJoinBuilderToRootComposer,
            joinBuilder: joinBuilder,
            $removeJoinBuilderFromRootComposer:
                $removeJoinBuilderFromRootComposer,
          ),
    );
    return composer;
  }
}

class $$TopicTestResultsTableTableManager
    extends
        RootTableManager<
          _$AppDatabase,
          $TopicTestResultsTable,
          TopicResultRow,
          $$TopicTestResultsTableFilterComposer,
          $$TopicTestResultsTableOrderingComposer,
          $$TopicTestResultsTableAnnotationComposer,
          $$TopicTestResultsTableCreateCompanionBuilder,
          $$TopicTestResultsTableUpdateCompanionBuilder,
          (TopicResultRow, $$TopicTestResultsTableReferences),
          TopicResultRow,
          PrefetchHooks Function({bool testId})
        > {
  $$TopicTestResultsTableTableManager(
    _$AppDatabase db,
    $TopicTestResultsTable table,
  ) : super(
        TableManagerState(
          db: db,
          table: table,
          createFilteringComposer: () =>
              $$TopicTestResultsTableFilterComposer($db: db, $table: table),
          createOrderingComposer: () =>
              $$TopicTestResultsTableOrderingComposer($db: db, $table: table),
          createComputedFieldComposer: () =>
              $$TopicTestResultsTableAnnotationComposer($db: db, $table: table),
          updateCompanionCallback:
              ({
                Value<String> testId = const Value.absent(),
                Value<String> topicId = const Value.absent(),
                Value<int> total = const Value.absent(),
                Value<int> correct = const Value.absent(),
                Value<int> wrong = const Value.absent(),
                Value<int> blank = const Value.absent(),
                Value<int?> mistakeKnowledge = const Value.absent(),
                Value<int?> mistakeAttention = const Value.absent(),
                Value<int?> mistakeTime = const Value.absent(),
                Value<int> rowid = const Value.absent(),
              }) => TopicTestResultsCompanion(
                testId: testId,
                topicId: topicId,
                total: total,
                correct: correct,
                wrong: wrong,
                blank: blank,
                mistakeKnowledge: mistakeKnowledge,
                mistakeAttention: mistakeAttention,
                mistakeTime: mistakeTime,
                rowid: rowid,
              ),
          createCompanionCallback:
              ({
                required String testId,
                required String topicId,
                required int total,
                required int correct,
                required int wrong,
                required int blank,
                Value<int?> mistakeKnowledge = const Value.absent(),
                Value<int?> mistakeAttention = const Value.absent(),
                Value<int?> mistakeTime = const Value.absent(),
                Value<int> rowid = const Value.absent(),
              }) => TopicTestResultsCompanion.insert(
                testId: testId,
                topicId: topicId,
                total: total,
                correct: correct,
                wrong: wrong,
                blank: blank,
                mistakeKnowledge: mistakeKnowledge,
                mistakeAttention: mistakeAttention,
                mistakeTime: mistakeTime,
                rowid: rowid,
              ),
          withReferenceMapper: (p0) => p0
              .map(
                (e) => (
                  e.readTable<$TopicTestResultsTable, TopicResultRow>(table),
                  $$TopicTestResultsTableReferences(db, table, e),
                ),
              )
              .toList(),
          prefetchHooksCallback: ({testId = false}) {
            return PrefetchHooks(
              db: db,
              explicitlyWatchedTables: [],
              addJoins:
                  <
                    T extends TableManagerState<
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic,
                      dynamic
                    >
                  >(state) {
                    if (testId) {
                      state = state.withJoin(
                        currentTable: table,
                        currentColumn: table.testId,
                        referencedTable: $$TopicTestResultsTableReferences
                            ._testIdTable(db),
                        referencedColumn: $$TopicTestResultsTableReferences
                            ._testIdTable(db)
                            .id,
                      ) as T;
                    }

                    return state;
                  },
              getPrefetchedDataCallback: (items) async {
                return [];
              },
            );
          },
        ),
      );
}

typedef $$TopicTestResultsTableProcessedTableManager =
    ProcessedTableManager<
      _$AppDatabase,
      $TopicTestResultsTable,
      TopicResultRow,
      $$TopicTestResultsTableFilterComposer,
      $$TopicTestResultsTableOrderingComposer,
      $$TopicTestResultsTableAnnotationComposer,
      $$TopicTestResultsTableCreateCompanionBuilder,
      $$TopicTestResultsTableUpdateCompanionBuilder,
      (TopicResultRow, $$TopicTestResultsTableReferences),
      TopicResultRow,
      PrefetchHooks Function({bool testId})
    >;
typedef $$OutboxTableCreateCompanionBuilder = OutboxCompanion Function({
  Value<int> id,
  required String entityType,
  required String entityId,
  required String operation,
  Value<String?> payload,
  Value<int> revision,
  Value<bool> rejected,
  Value<String?> lastError,
});
typedef $$OutboxTableUpdateCompanionBuilder = OutboxCompanion Function({
  Value<int> id,
  Value<String> entityType,
  Value<String> entityId,
  Value<String> operation,
  Value<String?> payload,
  Value<int> revision,
  Value<bool> rejected,
  Value<String?> lastError,
});

class $$OutboxTableFilterComposer
    extends Composer<_$AppDatabase, $OutboxTable> {
  $$OutboxTableFilterComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnFilters<int> get id => $composableBuilder(
    column: $table.id,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get entityType => $composableBuilder(
    column: $table.entityType,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get entityId => $composableBuilder(
    column: $table.entityId,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get operation => $composableBuilder(
    column: $table.operation,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get payload => $composableBuilder(
    column: $table.payload,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get revision => $composableBuilder(
    column: $table.revision,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<bool> get rejected => $composableBuilder(
    column: $table.rejected,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get lastError => $composableBuilder(
    column: $table.lastError,
    builder: (column) => ColumnFilters(column),
  );
}

class $$OutboxTableOrderingComposer
    extends Composer<_$AppDatabase, $OutboxTable> {
  $$OutboxTableOrderingComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnOrderings<int> get id => $composableBuilder(
    column: $table.id,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get entityType => $composableBuilder(
    column: $table.entityType,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get entityId => $composableBuilder(
    column: $table.entityId,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get operation => $composableBuilder(
    column: $table.operation,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get payload => $composableBuilder(
    column: $table.payload,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get revision => $composableBuilder(
    column: $table.revision,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<bool> get rejected => $composableBuilder(
    column: $table.rejected,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get lastError => $composableBuilder(
    column: $table.lastError,
    builder: (column) => ColumnOrderings(column),
  );
}

class $$OutboxTableAnnotationComposer
    extends Composer<_$AppDatabase, $OutboxTable> {
  $$OutboxTableAnnotationComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  GeneratedColumn<int> get id =>
      $composableBuilder(column: $table.id, builder: (column) => column);

  GeneratedColumn<String> get entityType => $composableBuilder(
    column: $table.entityType,
    builder: (column) => column,
  );

  GeneratedColumn<String> get entityId =>
      $composableBuilder(column: $table.entityId, builder: (column) => column);

  GeneratedColumn<String> get operation =>
      $composableBuilder(column: $table.operation, builder: (column) => column);

  GeneratedColumn<String> get payload =>
      $composableBuilder(column: $table.payload, builder: (column) => column);

  GeneratedColumn<int> get revision =>
      $composableBuilder(column: $table.revision, builder: (column) => column);

  GeneratedColumn<bool> get rejected =>
      $composableBuilder(column: $table.rejected, builder: (column) => column);

  GeneratedColumn<String> get lastError =>
      $composableBuilder(column: $table.lastError, builder: (column) => column);
}

class $$OutboxTableTableManager
    extends
        RootTableManager<
          _$AppDatabase,
          $OutboxTable,
          OutboxRow,
          $$OutboxTableFilterComposer,
          $$OutboxTableOrderingComposer,
          $$OutboxTableAnnotationComposer,
          $$OutboxTableCreateCompanionBuilder,
          $$OutboxTableUpdateCompanionBuilder,
          (OutboxRow, BaseReferences<_$AppDatabase, $OutboxTable, OutboxRow>),
          OutboxRow,
          PrefetchHooks Function()
        > {
  $$OutboxTableTableManager(_$AppDatabase db, $OutboxTable table)
    : super(
        TableManagerState(
          db: db,
          table: table,
          createFilteringComposer: () =>
              $$OutboxTableFilterComposer($db: db, $table: table),
          createOrderingComposer: () =>
              $$OutboxTableOrderingComposer($db: db, $table: table),
          createComputedFieldComposer: () =>
              $$OutboxTableAnnotationComposer($db: db, $table: table),
          updateCompanionCallback:
              ({
                Value<int> id = const Value.absent(),
                Value<String> entityType = const Value.absent(),
                Value<String> entityId = const Value.absent(),
                Value<String> operation = const Value.absent(),
                Value<String?> payload = const Value.absent(),
                Value<int> revision = const Value.absent(),
                Value<bool> rejected = const Value.absent(),
                Value<String?> lastError = const Value.absent(),
              }) => OutboxCompanion(
                id: id,
                entityType: entityType,
                entityId: entityId,
                operation: operation,
                payload: payload,
                revision: revision,
                rejected: rejected,
                lastError: lastError,
              ),
          createCompanionCallback:
              ({
                Value<int> id = const Value.absent(),
                required String entityType,
                required String entityId,
                required String operation,
                Value<String?> payload = const Value.absent(),
                Value<int> revision = const Value.absent(),
                Value<bool> rejected = const Value.absent(),
                Value<String?> lastError = const Value.absent(),
              }) => OutboxCompanion.insert(
                id: id,
                entityType: entityType,
                entityId: entityId,
                operation: operation,
                payload: payload,
                revision: revision,
                rejected: rejected,
                lastError: lastError,
              ),
          withReferenceMapper: (p0) => p0
              .map(
                (e) => (
                  e.readTable<$OutboxTable, OutboxRow>(table),
                  BaseReferences<_$AppDatabase, $OutboxTable, OutboxRow>(
                    db,
                    table,
                    e,
                  ),
                ),
              )
              .toList(),
          prefetchHooksCallback: null,
        ),
      );
}

typedef $$OutboxTableProcessedTableManager =
    ProcessedTableManager<
      _$AppDatabase,
      $OutboxTable,
      OutboxRow,
      $$OutboxTableFilterComposer,
      $$OutboxTableOrderingComposer,
      $$OutboxTableAnnotationComposer,
      $$OutboxTableCreateCompanionBuilder,
      $$OutboxTableUpdateCompanionBuilder,
      (OutboxRow, BaseReferences<_$AppDatabase, $OutboxTable, OutboxRow>),
      OutboxRow,
      PrefetchHooks Function()
    >;
typedef $$SyncCursorsTableCreateCompanionBuilder =
    SyncCursorsCompanion Function({
      required String stream,
      required int cursor,
      Value<int> rowid,
    });
typedef $$SyncCursorsTableUpdateCompanionBuilder =
    SyncCursorsCompanion Function({
      Value<String> stream,
      Value<int> cursor,
      Value<int> rowid,
    });

class $$SyncCursorsTableFilterComposer
    extends Composer<_$AppDatabase, $SyncCursorsTable> {
  $$SyncCursorsTableFilterComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnFilters<String> get stream => $composableBuilder(
    column: $table.stream,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<int> get cursor => $composableBuilder(
    column: $table.cursor,
    builder: (column) => ColumnFilters(column),
  );
}

class $$SyncCursorsTableOrderingComposer
    extends Composer<_$AppDatabase, $SyncCursorsTable> {
  $$SyncCursorsTableOrderingComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnOrderings<String> get stream => $composableBuilder(
    column: $table.stream,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<int> get cursor => $composableBuilder(
    column: $table.cursor,
    builder: (column) => ColumnOrderings(column),
  );
}

class $$SyncCursorsTableAnnotationComposer
    extends Composer<_$AppDatabase, $SyncCursorsTable> {
  $$SyncCursorsTableAnnotationComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  GeneratedColumn<String> get stream =>
      $composableBuilder(column: $table.stream, builder: (column) => column);

  GeneratedColumn<int> get cursor =>
      $composableBuilder(column: $table.cursor, builder: (column) => column);
}

class $$SyncCursorsTableTableManager
    extends
        RootTableManager<
          _$AppDatabase,
          $SyncCursorsTable,
          SyncCursorRow,
          $$SyncCursorsTableFilterComposer,
          $$SyncCursorsTableOrderingComposer,
          $$SyncCursorsTableAnnotationComposer,
          $$SyncCursorsTableCreateCompanionBuilder,
          $$SyncCursorsTableUpdateCompanionBuilder,
          (
            SyncCursorRow,
            BaseReferences<_$AppDatabase, $SyncCursorsTable, SyncCursorRow>,
          ),
          SyncCursorRow,
          PrefetchHooks Function()
        > {
  $$SyncCursorsTableTableManager(_$AppDatabase db, $SyncCursorsTable table)
    : super(
        TableManagerState(
          db: db,
          table: table,
          createFilteringComposer: () =>
              $$SyncCursorsTableFilterComposer($db: db, $table: table),
          createOrderingComposer: () =>
              $$SyncCursorsTableOrderingComposer($db: db, $table: table),
          createComputedFieldComposer: () =>
              $$SyncCursorsTableAnnotationComposer($db: db, $table: table),
          updateCompanionCallback:
              ({
                Value<String> stream = const Value.absent(),
                Value<int> cursor = const Value.absent(),
                Value<int> rowid = const Value.absent(),
              }) => SyncCursorsCompanion(
                stream: stream,
                cursor: cursor,
                rowid: rowid,
              ),
          createCompanionCallback:
              ({
                required String stream,
                required int cursor,
                Value<int> rowid = const Value.absent(),
              }) => SyncCursorsCompanion.insert(
                stream: stream,
                cursor: cursor,
                rowid: rowid,
              ),
          withReferenceMapper: (p0) => p0
              .map(
                (e) => (
                  e.readTable<$SyncCursorsTable, SyncCursorRow>(table),
                  BaseReferences<
                    _$AppDatabase,
                    $SyncCursorsTable,
                    SyncCursorRow
                  >(db, table, e),
                ),
              )
              .toList(),
          prefetchHooksCallback: null,
        ),
      );
}

typedef $$SyncCursorsTableProcessedTableManager =
    ProcessedTableManager<
      _$AppDatabase,
      $SyncCursorsTable,
      SyncCursorRow,
      $$SyncCursorsTableFilterComposer,
      $$SyncCursorsTableOrderingComposer,
      $$SyncCursorsTableAnnotationComposer,
      $$SyncCursorsTableCreateCompanionBuilder,
      $$SyncCursorsTableUpdateCompanionBuilder,
      (
        SyncCursorRow,
        BaseReferences<_$AppDatabase, $SyncCursorsTable, SyncCursorRow>,
      ),
      SyncCursorRow,
      PrefetchHooks Function()
    >;
typedef $$CacheEntriesTableCreateCompanionBuilder =
    CacheEntriesCompanion Function({
      required String key,
      required String value,
      Value<int> rowid,
    });
typedef $$CacheEntriesTableUpdateCompanionBuilder =
    CacheEntriesCompanion Function({
      Value<String> key,
      Value<String> value,
      Value<int> rowid,
    });

class $$CacheEntriesTableFilterComposer
    extends Composer<_$AppDatabase, $CacheEntriesTable> {
  $$CacheEntriesTableFilterComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnFilters<String> get key => $composableBuilder(
    column: $table.key,
    builder: (column) => ColumnFilters(column),
  );

  ColumnFilters<String> get value => $composableBuilder(
    column: $table.value,
    builder: (column) => ColumnFilters(column),
  );
}

class $$CacheEntriesTableOrderingComposer
    extends Composer<_$AppDatabase, $CacheEntriesTable> {
  $$CacheEntriesTableOrderingComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  ColumnOrderings<String> get key => $composableBuilder(
    column: $table.key,
    builder: (column) => ColumnOrderings(column),
  );

  ColumnOrderings<String> get value => $composableBuilder(
    column: $table.value,
    builder: (column) => ColumnOrderings(column),
  );
}

class $$CacheEntriesTableAnnotationComposer
    extends Composer<_$AppDatabase, $CacheEntriesTable> {
  $$CacheEntriesTableAnnotationComposer({
    required super.$db,
    required super.$table,
    super.joinBuilder,
    super.$addJoinBuilderToRootComposer,
    super.$removeJoinBuilderFromRootComposer,
  });
  GeneratedColumn<String> get key =>
      $composableBuilder(column: $table.key, builder: (column) => column);

  GeneratedColumn<String> get value =>
      $composableBuilder(column: $table.value, builder: (column) => column);
}

class $$CacheEntriesTableTableManager
    extends
        RootTableManager<
          _$AppDatabase,
          $CacheEntriesTable,
          CacheEntryRow,
          $$CacheEntriesTableFilterComposer,
          $$CacheEntriesTableOrderingComposer,
          $$CacheEntriesTableAnnotationComposer,
          $$CacheEntriesTableCreateCompanionBuilder,
          $$CacheEntriesTableUpdateCompanionBuilder,
          (
            CacheEntryRow,
            BaseReferences<_$AppDatabase, $CacheEntriesTable, CacheEntryRow>,
          ),
          CacheEntryRow,
          PrefetchHooks Function()
        > {
  $$CacheEntriesTableTableManager(_$AppDatabase db, $CacheEntriesTable table)
    : super(
        TableManagerState(
          db: db,
          table: table,
          createFilteringComposer: () =>
              $$CacheEntriesTableFilterComposer($db: db, $table: table),
          createOrderingComposer: () =>
              $$CacheEntriesTableOrderingComposer($db: db, $table: table),
          createComputedFieldComposer: () =>
              $$CacheEntriesTableAnnotationComposer($db: db, $table: table),
          updateCompanionCallback: ({
            Value<String> key = const Value.absent(),
            Value<String> value = const Value.absent(),
            Value<int> rowid = const Value.absent(),
          }) => CacheEntriesCompanion(key: key, value: value, rowid: rowid),
          createCompanionCallback:
              ({
                required String key,
                required String value,
                Value<int> rowid = const Value.absent(),
              }) => CacheEntriesCompanion.insert(
                key: key,
                value: value,
                rowid: rowid,
              ),
          withReferenceMapper: (p0) => p0
              .map(
                (e) => (
                  e.readTable<$CacheEntriesTable, CacheEntryRow>(table),
                  BaseReferences<
                    _$AppDatabase,
                    $CacheEntriesTable,
                    CacheEntryRow
                  >(db, table, e),
                ),
              )
              .toList(),
          prefetchHooksCallback: null,
        ),
      );
}

typedef $$CacheEntriesTableProcessedTableManager =
    ProcessedTableManager<
      _$AppDatabase,
      $CacheEntriesTable,
      CacheEntryRow,
      $$CacheEntriesTableFilterComposer,
      $$CacheEntriesTableOrderingComposer,
      $$CacheEntriesTableAnnotationComposer,
      $$CacheEntriesTableCreateCompanionBuilder,
      $$CacheEntriesTableUpdateCompanionBuilder,
      (
        CacheEntryRow,
        BaseReferences<_$AppDatabase, $CacheEntriesTable, CacheEntryRow>,
      ),
      CacheEntryRow,
      PrefetchHooks Function()
    >;

class $AppDatabaseManager {
  final _$AppDatabase _db;
  $AppDatabaseManager(this._db);
  $$TopicTestsTableTableManager get topicTests =>
      $$TopicTestsTableTableManager(_db, _db.topicTests);
  $$TopicTestResultsTableTableManager get topicTestResults =>
      $$TopicTestResultsTableTableManager(_db, _db.topicTestResults);
  $$OutboxTableTableManager get outbox =>
      $$OutboxTableTableManager(_db, _db.outbox);
  $$SyncCursorsTableTableManager get syncCursors =>
      $$SyncCursorsTableTableManager(_db, _db.syncCursors);
  $$CacheEntriesTableTableManager get cacheEntries =>
      $$CacheEntriesTableTableManager(_db, _db.cacheEntries);
}
