import { useEffect } from 'react'
import { DatePicker, Form, Input, InputNumber, Modal, Select, Space, Button } from 'antd'
import { MinusCircleOutlined, PlusOutlined } from '@ant-design/icons'
import dayjs, { type Dayjs } from 'dayjs'
import utc from 'dayjs/plugin/utc'
import type { Schemas } from '../api/client'

dayjs.extend(utc)

/** ÖSYM dates are Istanbul wall time; the picker edits them as such whatever the browser's zone. */
const istanbul = 180

type ModalProps<T> = { open: boolean; title: string; initial?: Partial<T>; onCancel: () => void; onSubmit: (value: T) => void }

function useReset<T>(form: ReturnType<typeof Form.useForm<T>>[0], open: boolean, initial: unknown) {
  useEffect(() => {
    if (open) {
      form.resetFields()
      form.setFieldsValue(initial as never)
    }
  }, [form, open, initial])
}

export type TopicValues = {
  name: string
  subtopics: string[]
  gradeMin: number
  gradeMax: number
  description?: string | null
  sortOrder?: number | null
}

const grades = [9, 10, 11, 12].map((g) => ({ value: g, label: `${g}. sınıf` }))

export function TopicModal({ open, title, initial, onCancel, onSubmit }: ModalProps<TopicValues>) {
  const [form] = Form.useForm<TopicValues>()
  useReset(form, open, initial)
  return (
    <Modal open={open} title={title} onCancel={onCancel} onOk={() => form.submit()} okText="Taslağa ekle" destroyOnHidden>
      <Form form={form} layout="vertical" onFinish={onSubmit} initialValues={{ gradeMin: 9, gradeMax: 9, subtopics: [] }}>
        <Form.Item name="name" label="Konu adı" rules={[{ required: true, max: 120, whitespace: true }]}>
          <Input />
        </Form.Item>
        <Form.Item
          name="subtopics"
          label="Alt konular"
          extra="Enter ile ekle; en çok 30, ';' içermez."
          rules={[{ required: true, type: 'array', min: 1, max: 30 }]}
        >
          <Select mode="tags" tokenSeparators={[]} open={false} />
        </Form.Item>
        <Space>
          <Form.Item name="gradeMin" label="İlk sınıf" rules={[{ required: true }]}>
            <Select options={grades} style={{ width: 120 }} />
          </Form.Item>
          <Form.Item
            name="gradeMax"
            label="Son sınıf"
            dependencies={['gradeMin']}
            rules={[
              { required: true },
              ({ getFieldValue }) => ({
                validator: (_, v) => (v >= getFieldValue('gradeMin') ? Promise.resolve() : Promise.reject(new Error('İlk sınıftan küçük olamaz'))),
              }),
            ]}
          >
            <Select options={grades} style={{ width: 120 }} />
          </Form.Item>
          <Form.Item name="sortOrder" label="Sıra" extra="Boş: sona / yerinde kalır">
            <InputNumber min={0} />
          </Form.Item>
        </Space>
        <Form.Item name="description" label="Açıklama" rules={[{ max: 1000 }]}>
          <Input.TextArea rows={3} />
        </Form.Item>
      </Form>
    </Modal>
  )
}

export type LinkValues = { from: string[]; to: string[] }

/** Merge (many → one) or split (one → many); the API checks the codes are active and in one subject. */
export function LinkModal({
  open,
  mode,
  options,
  onCancel,
  onSubmit,
}: {
  open: boolean
  mode: 'merge' | 'split'
  options: { value: string; label: string }[]
  onCancel: () => void
  onSubmit: (value: LinkValues) => void
}) {
  const [form] = Form.useForm<LinkValues>()
  useReset(form, open, {})
  const merge = mode === 'merge'
  return (
    <Modal
      open={open}
      title={merge ? 'Konuları birleştir' : 'Konuyu böl'}
      onCancel={onCancel}
      onOk={() => form.submit()}
      okText="Taslağa ekle"
      destroyOnHidden
    >
      <Form form={form} layout="vertical" onFinish={onSubmit}>
        <Form.Item
          name="from"
          label={merge ? 'Birleşecek konular (emekliye ayrılır)' : 'Bölünecek konu (emekliye ayrılır)'}
          rules={[{ required: true, type: 'array', min: 1, max: merge ? 20 : 1 }]}
        >
          <Select mode="multiple" options={options} showSearch optionFilterProp="label" maxCount={merge ? undefined : 1} />
        </Form.Item>
        <Form.Item
          name="to"
          label={merge ? 'Hedef konu (kayıtlar buna sayılır)' : 'Yeni konular (önce taslağa eklenmiş olmalı)'}
          rules={[{ required: true, type: 'array', min: merge ? 1 : 2, max: merge ? 1 : 20 }]}
        >
          <Select mode="multiple" options={options} showSearch optionFilterProp="label" maxCount={merge ? 1 : undefined} />
        </Form.Item>
      </Form>
    </Modal>
  )
}

export type SourceValues = { code: string; name: string; kind: Schemas['SourceKind'] }

const kinds = [
  { value: 'Publisher', label: 'Yayınevi' },
  { value: 'Official', label: 'Resmi (ÖSYM, MEB)' },
  { value: 'Institution', label: 'Kurum' },
]

export function SourceModal({ open, title, initial, onCancel, onSubmit, editing }: ModalProps<SourceValues> & { editing: boolean }) {
  const [form] = Form.useForm<SourceValues>()
  useReset(form, open, initial)
  return (
    <Modal open={open} title={title} onCancel={onCancel} onOk={() => form.submit()} okText="Taslağa ekle" destroyOnHidden>
      <Form form={form} layout="vertical" onFinish={onSubmit} initialValues={{ kind: 'Publisher' }}>
        <Form.Item
          name="code"
          label="Kod"
          extra="Kalıcıdır: büyük harf, rakam ve _"
          rules={[{ required: true, pattern: /^[A-Z0-9_]+$/, max: 40 }]}
        >
          <Input disabled={editing} />
        </Form.Item>
        <Form.Item name="name" label="Ad" rules={[{ required: true, max: 120, whitespace: true }]}>
          <Input />
        </Form.Item>
        <Form.Item name="kind" label="Tür" rules={[{ required: true }]}>
          <Select options={kinds} />
        </Form.Item>
      </Form>
    </Modal>
  )
}

type EventValues = { code: string; name: string; startsAt: Dayjs; endsAt?: Dayjs | null; status: Schemas['CalendarEventStatus'] }
type CalendarValues = { examYear: number; sourceUrl: string; note?: string | null; events: EventValues[] }

const eventCodes = ['application_start', 'application_end', 'late_application', 'tyt', 'ayt', 'ydt', 'results'].map((c) => ({ value: c, label: c }))

const toPicker = (iso: string) => dayjs(iso).utcOffset(istanbul)
const fromPicker = (d: Dayjs) => d.format('YYYY-MM-DDTHH:mm:ss') + '+03:00'

function toCalendarValues(c: Schemas['ExamCalendar']): CalendarValues {
  return {
    examYear: Number(c.examYear),
    sourceUrl: c.sourceUrl,
    note: c.note,
    events: c.events.map((e) => ({ ...e, startsAt: toPicker(e.startsAt), endsAt: e.endsAt ? toPicker(e.endsAt) : null })),
  }
}

export function CalendarModal({ open, title, initial, onCancel, onSubmit }: ModalProps<Schemas['ExamCalendar']>) {
  const [form] = Form.useForm<CalendarValues>()
  useReset(form, open, initial ? toCalendarValues(initial as Schemas['ExamCalendar']) : { events: [] })
  return (
    <Modal open={open} title={title} width={860} onCancel={onCancel} onOk={() => form.submit()} okText="Taslağa ekle" destroyOnHidden>
      <Form
        form={form}
        layout="vertical"
        onFinish={(v) =>
          onSubmit({
            examYear: v.examYear,
            sourceUrl: v.sourceUrl,
            note: v.note || null,
            events: v.events.map((e) => ({ ...e, startsAt: fromPicker(e.startsAt), endsAt: e.endsAt ? fromPicker(e.endsAt) : null })),
          })
        }
      >
        <Space wrap>
          <Form.Item name="examYear" label="Sınav yılı" rules={[{ required: true }]}>
            <InputNumber min={2020} max={2100} />
          </Form.Item>
          <Form.Item name="sourceUrl" label="Kaynak adresi" rules={[{ required: true, type: 'url', max: 300 }]} style={{ width: 420 }}>
            <Input />
          </Form.Item>
        </Space>
        <Form.Item name="note" label="Not" rules={[{ max: 500 }]}>
          <Input.TextArea rows={2} />
        </Form.Item>
        <Form.List name="events">
          {(fields, { add, remove }) => (
            <>
              {fields.map((field) => (
                <Space key={field.key} align="baseline" wrap size={6}>
                  <Form.Item name={[field.name, 'code']} rules={[{ required: true }]}>
                    <Select options={eventCodes} placeholder="kod" style={{ width: 150 }} />
                  </Form.Item>
                  <Form.Item name={[field.name, 'name']} rules={[{ required: true, max: 60 }]}>
                    <Input placeholder="ad" style={{ width: 120 }} />
                  </Form.Item>
                  <Form.Item name={[field.name, 'startsAt']} rules={[{ required: true }]}>
                    <DatePicker showTime={{ format: 'HH:mm' }} format="YYYY-MM-DD HH:mm" placeholder="başlangıç (TR)" style={{ width: 170 }} />
                  </Form.Item>
                  <Form.Item name={[field.name, 'endsAt']}>
                    <DatePicker showTime={{ format: 'HH:mm' }} format="YYYY-MM-DD HH:mm" placeholder="bitiş (ops.)" style={{ width: 170 }} />
                  </Form.Item>
                  <Form.Item name={[field.name, 'status']} rules={[{ required: true }]} initialValue="Estimated">
                    <Select
                      style={{ width: 120 }}
                      options={[
                        { value: 'Announced', label: 'Açıklandı' },
                        { value: 'Estimated', label: 'Tahmini' },
                      ]}
                    />
                  </Form.Item>
                  <MinusCircleOutlined onClick={() => remove(field.name)} aria-label="Tarihi sil" />
                </Space>
              ))}
              <Button type="dashed" onClick={() => add()} icon={<PlusOutlined />}>
                Tarih ekle
              </Button>
            </>
          )}
        </Form.List>
      </Form>
    </Modal>
  )
}
