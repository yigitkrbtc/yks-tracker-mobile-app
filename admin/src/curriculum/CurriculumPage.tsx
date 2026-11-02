import { useMemo, useState } from 'react'
import { Alert, App, Badge, Button, Card, Col, Descriptions, Empty, Input, Row, Select, Space, Spin, Table, Tabs, Tag, Tooltip, Typography } from 'antd'
import { DownloadOutlined, PlusOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import utc from 'dayjs/plugin/utc'
import { n, type Schemas } from '../api/client'
import { DraftPanel } from './DraftPanel'
import { CalendarModal, LinkModal, SourceModal, TopicModal, type SourceValues, type TopicValues } from './forms'
import { touchedTopics, type Operation } from './operations'
import { downloadSeedExport, useAdminCurriculum, useCreateDraft, useDraft, useSaveDraft, type Draft } from './queries'

type Topic = Schemas['AdminTopicDto'] & { subjectCode: string }
type Source = Schemas['AdminSourceDto']

type Editing =
  | { kind: 'topic'; subjectCode: string; topic?: Topic }
  | { kind: 'link'; mode: 'merge' | 'split' }
  | { kind: 'source'; source?: Source }
  | { kind: 'calendar'; calendar?: Schemas['ExamCalendar'] }
  | null

dayjs.extend(utc)

/** ÖSYM dates in Istanbul time, whatever the browser's zone. */
const trTime = (iso: string) => dayjs(iso).utcOffset(180).format('DD.MM.YYYY HH:mm')

const tierColor: Record<string, string> = { High: 'red', Medium: 'orange', Low: 'blue', Rare: 'default' }

export function CurriculumPage() {
  const { message } = App.useApp()
  const curriculum = useAdminCurriculum()
  const [selectedDraft, setSelectedDraft] = useState<string>()
  const drafts = curriculum.data?.drafts ?? []
  const draftId = drafts.find((d) => d.id === selectedDraft)?.id ?? drafts.find((d) => d.kind === 'Manual')?.id ?? drafts[0]?.id
  const draft = useDraft(draftId)
  const create = useCreateDraft()
  const save = useSaveDraft()
  const [editing, setEditing] = useState<Editing>(null)
  const [subject, setSubject] = useState<string>()
  const [search, setSearch] = useState('')

  const topics = useMemo<Topic[]>(
    () => curriculum.data?.subjects.flatMap((s) => s.topics.map((t) => ({ ...t, subjectCode: s.code }))) ?? [],
    [curriculum.data],
  )
  const touched = useMemo(() => new Set(draft.data?.operations.flatMap(touchedTopics) ?? []), [draft.data])

  if (curriculum.isLoading) return <Spin />
  if (curriculum.error || !curriculum.data) return <Alert type="error" showIcon message={curriculum.error?.message ?? 'Müfredat yüklenemedi.'} />
  const data = curriculum.data

  const editable = draft.data && draft.data.kind === 'Manual' ? draft.data : null

  /** Appends to the open draft; opens one first if needed. */
  async function add(...ops: Operation[]) {
    let target: Draft | undefined = editable ?? undefined
    if (!target) {
      try {
        target = await create.mutateAsync(null)
        setSelectedDraft(target.id)
      } catch (e) {
        message.error((e as Error).message)
        return
      }
    }
    save.mutate(
      { draft: target, operations: [...target.operations, ...ops] },
      {
        onSuccess: (d) => (d.validation.isValid ? message.success('Taslağa eklendi.') : message.warning(d.validation.problem ?? 'Taslakta sorun var.')),
        onError: (e) => message.error(e.message),
      },
    )
    setEditing(null)
  }

  const subjectOptions = data.subjects.map((s) => ({ value: s.code, label: `${s.code} — ${s.name}` }))
  const visible = topics.filter(
    (t) =>
      (!subject || t.subjectCode === subject) &&
      (!search || `${t.code} ${t.name} ${t.subtopics.join(' ')}`.toLocaleLowerCase('tr').includes(search.toLocaleLowerCase('tr'))),
  )
  const draftCodes = (editable?.operations ?? []).flatMap((o) => (o.type === 'topic.add' && o.code ? [{ code: o.code, name: o.name, subjectCode: o.subjectCode }] : []))
  const linkOptions = [
    ...topics.filter((t) => t.isActive && (!subject || t.subjectCode === subject)).map((t) => ({ value: t.code, label: `${t.code} ${t.name}` })),
    ...draftCodes.filter((t) => !subject || t.subjectCode === subject).map((t) => ({ value: t.code, label: `${t.code} ${t.name} (taslak)` })),
  ]

  const topicTab = (
    <Space direction="vertical" style={{ width: '100%' }}>
      <Space wrap>
        <Select allowClear placeholder="Ders" options={subjectOptions} value={subject} onChange={setSubject} style={{ width: 280 }} showSearch optionFilterProp="label" />
        <Input.Search allowClear placeholder="Kod, ad veya alt konu" onChange={(e) => setSearch(e.target.value)} style={{ width: 260 }} />
        <Tooltip title={subject ? undefined : 'Önce ders seç'}>
          <Button icon={<PlusOutlined />} disabled={!subject} onClick={() => setEditing({ kind: 'topic', subjectCode: subject! })}>
            Yeni konu
          </Button>
        </Tooltip>
        <Button onClick={() => setEditing({ kind: 'link', mode: 'merge' })}>Birleştir</Button>
        <Button onClick={() => setEditing({ kind: 'link', mode: 'split' })}>Böl</Button>
      </Space>
      <Table<Topic>
        size="small"
        rowKey="code"
        dataSource={visible}
        pagination={{ pageSize: 50, showSizeChanger: false }}
        scroll={{ x: 900 }}
        columns={[
          {
            title: 'Kod',
            dataIndex: 'code',
            width: 140,
            render: (code: string) => (touched.has(code) ? <Badge status="processing" text={code} /> : code),
          },
          {
            title: 'Konu',
            dataIndex: 'name',
            render: (_, t) => (
              <Space direction="vertical" size={0}>
                <Typography.Text delete={!t.isActive}>{t.name}</Typography.Text>
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  {t.subtopics.join(' · ')}
                </Typography.Text>
              </Space>
            ),
          },
          { title: 'Sınıf', width: 80, render: (_, t) => (t.gradeMin === t.gradeMax ? t.gradeMin : `${t.gradeMin}–${t.gradeMax}`) },
          {
            title: 'Beklenen soru (test)',
            width: 220,
            render: (_, t) =>
              t.weights.length === 0 ? (
                <Tooltip title="Seed bu konuyu bilmiyor; q = 0 (ağırlık seed'den gelir).">
                  <Tag>yok</Tag>
                </Tooltip>
              ) : (
                t.weights.map((w) => (
                  <Tag key={w.testCode} color={tierColor[w.tier]}>
                    {w.testCode} {n(w.expectedQuestions).toLocaleString('tr', { maximumFractionDigits: 2 })}
                  </Tag>
                ))
              ),
          },
          { title: 'Durum', width: 90, render: (_, t) => (t.isActive ? <Tag color="green">aktif</Tag> : <Tag>emekli</Tag>) },
          {
            title: '',
            width: 190,
            render: (_, t) => (
              <Space size={0}>
                <Button type="link" size="small" onClick={() => setEditing({ kind: 'topic', subjectCode: t.subjectCode, topic: t })}>
                  Düzenle
                </Button>
                {t.isActive ? (
                  <Button type="link" size="small" danger onClick={() => add({ type: 'topic.retire', code: t.code })}>
                    Emekliye ayır
                  </Button>
                ) : (
                  <Button type="link" size="small" onClick={() => add({ type: 'topic.reactivate', code: t.code })}>
                    Aktifleştir
                  </Button>
                )}
              </Space>
            ),
          },
        ]}
      />
    </Space>
  )

  const sourceTab = (
    <Space direction="vertical" style={{ width: '100%' }}>
      <Button icon={<PlusOutlined />} onClick={() => setEditing({ kind: 'source' })}>
        Yeni kaynak
      </Button>
      <Table<Source>
        size="small"
        rowKey="code"
        dataSource={data.sources}
        pagination={false}
        columns={[
          { title: 'Kod', dataIndex: 'code', width: 160 },
          { title: 'Ad', dataIndex: 'name', render: (name: string, s) => <Typography.Text delete={!s.isActive}>{name}</Typography.Text> },
          { title: 'Tür', dataIndex: 'kind', width: 120 },
          { title: 'Durum', width: 90, render: (_, s) => (s.isActive ? <Tag color="green">aktif</Tag> : <Tag>emekli</Tag>) },
          {
            title: '',
            width: 190,
            render: (_, s) => (
              <Space size={0}>
                <Button type="link" size="small" onClick={() => setEditing({ kind: 'source', source: s })}>
                  Düzenle
                </Button>
                {s.isActive ? (
                  <Button type="link" size="small" danger onClick={() => add({ type: 'source.retire', code: s.code })}>
                    Emekliye ayır
                  </Button>
                ) : (
                  <Button type="link" size="small" onClick={() => add({ type: 'source.reactivate', code: s.code })}>
                    Aktifleştir
                  </Button>
                )}
              </Space>
            ),
          },
        ]}
      />
    </Space>
  )

  const calendarTab = (
    <Space direction="vertical" style={{ width: '100%' }}>
      <Button icon={<PlusOutlined />} onClick={() => setEditing({ kind: 'calendar' })}>
        Yeni yıl
      </Button>
      {data.calendars.length === 0 && <Empty description="Takvim yok" />}
      {data.calendars.map((c) => (
        <Card
          key={n(c.examYear)}
          size="small"
          title={`YKS ${n(c.examYear)}`}
          extra={
            <Button type="link" onClick={() => setEditing({ kind: 'calendar', calendar: c })}>
              Düzenle
            </Button>
          }
        >
          <Descriptions size="small" column={1} items={c.events.map((e) => ({
            key: e.code,
            label: e.name,
            children: (
              <Space>
                {trTime(e.startsAt)}
                {e.endsAt && `– ${trTime(e.endsAt)}`}
                <Tag color={e.status === 'Announced' ? 'green' : 'default'}>{e.status === 'Announced' ? 'açıklandı' : 'tahmini'}</Tag>
              </Space>
            ),
          }))} />
          {c.note && <Typography.Paragraph type="secondary">{c.note}</Typography.Paragraph>}
        </Card>
      ))}
    </Space>
  )

  return (
    <Space direction="vertical" style={{ width: '100%' }} size="middle">
      <Space wrap style={{ justifyContent: 'space-between', width: '100%' }}>
        <Typography.Title level={4} style={{ margin: 0 }}>
          YKS {n(data.examYear)} müfredatı · {n(data.versionNumber)}. sürüm
        </Typography.Title>
        <Button
          icon={<DownloadOutlined />}
          onClick={() => downloadSeedExport().catch((e: Error) => message.error(e.message))}
          title="data/seed'e işlemek için konu, kaynak ve takvim dosyaları"
        >
          Seed olarak indir
        </Button>
      </Space>
      {drafts.some((d) => d.kind === 'SeedImport') && (
        <Alert
          type="info"
          showIcon
          message="Sunucudaki seed canlı müfredattan farklı; içe aktarma taslağı onay bekliyor."
          action={
            <Button size="small" onClick={() => setSelectedDraft(drafts.find((d) => d.kind === 'SeedImport')!.id)}>
              Göster
            </Button>
          }
        />
      )}
      <Row gutter={[16, 16]}>
        <Col xs={24} xl={15}>
          <Card size="small">
            <Tabs
              items={[
                { key: 'topics', label: `Konular (${topics.filter((t) => t.isActive).length})`, children: topicTab },
                { key: 'sources', label: `Kaynaklar (${data.sources.filter((s) => s.isActive).length})`, children: sourceTab },
                { key: 'calendar', label: 'Sınav takvimi', children: calendarTab },
              ]}
            />
          </Card>
        </Col>
        <Col xs={24} xl={9}>
          <Space direction="vertical" style={{ width: '100%', position: 'sticky', top: 16 }}>
            {drafts.length > 1 && (
              <Select
                style={{ width: '100%' }}
                value={draftId}
                onChange={setSelectedDraft}
                options={drafts.map((d) => ({ value: d.id, label: `${d.title} (${n(d.operationCount)} işlem)` }))}
              />
            )}
            {draft.data ? (
              <DraftPanel draft={draft.data} />
            ) : draftId ? (
              <Spin />
            ) : (
              <Card size="small">
                <Empty description="Açık taslak yok. Bir değişiklik yaptığında taslak kendiliğinden açılır.">
                  <Button onClick={() => create.mutate(null, { onError: (e) => message.error(e.message) })} loading={create.isPending}>
                    Boş taslak aç
                  </Button>
                </Empty>
              </Card>
            )}
          </Space>
        </Col>
      </Row>

      <TopicModal
        open={editing?.kind === 'topic'}
        title={editing?.kind === 'topic' && editing.topic ? `${editing.topic.code} düzenle` : `Yeni konu — ${editing?.kind === 'topic' ? editing.subjectCode : ''}`}
        initial={editing?.kind === 'topic' && editing.topic ? { ...editing.topic, gradeMin: n(editing.topic.gradeMin), gradeMax: n(editing.topic.gradeMax), sortOrder: null } : undefined}
        onCancel={() => setEditing(null)}
        onSubmit={(v: TopicValues) => {
          if (editing?.kind !== 'topic') return
          const fields = { name: v.name, subtopics: v.subtopics, gradeMin: v.gradeMin, gradeMax: v.gradeMax, description: v.description || null, sortOrder: v.sortOrder ?? null }
          void (editing.topic
            ? add({ type: 'topic.update', code: editing.topic.code, ...fields })
            : add({ type: 'topic.add', code: null, subjectCode: editing.subjectCode, ...fields }))
        }}
      />
      <LinkModal
        open={editing?.kind === 'link'}
        mode={editing?.kind === 'link' ? editing.mode : 'merge'}
        options={linkOptions}
        onCancel={() => setEditing(null)}
        onSubmit={(v) => {
          if (editing?.kind !== 'link') return
          void (editing.mode === 'merge'
            ? add({ type: 'topic.merge', fromCodes: v.from, toCode: v.to[0] })
            : add({ type: 'topic.split', fromCode: v.from[0], toCodes: v.to }))
        }}
      />
      <SourceModal
        open={editing?.kind === 'source'}
        editing={editing?.kind === 'source' && !!editing.source}
        title={editing?.kind === 'source' && editing.source ? `${editing.source.code} düzenle` : 'Yeni kaynak'}
        initial={editing?.kind === 'source' ? editing.source : undefined}
        onCancel={() => setEditing(null)}
        onSubmit={(v: SourceValues) => {
          if (editing?.kind !== 'source') return
          void add(editing.source ? { type: 'source.update', ...v } : { type: 'source.add', ...v })
        }}
      />
      <CalendarModal
        open={editing?.kind === 'calendar'}
        title={editing?.kind === 'calendar' && editing.calendar ? `YKS ${n(editing.calendar.examYear)} takvimi` : 'Yeni sınav yılı'}
        initial={editing?.kind === 'calendar' ? editing.calendar : undefined}
        onCancel={() => setEditing(null)}
        onSubmit={(calendar) => void add({ type: 'calendar.set', calendar })}
      />
    </Space>
  )
}
