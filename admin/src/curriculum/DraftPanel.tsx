import { useState } from 'react'
import { Alert, App, Button, Card, Empty, Input, List, Modal, Popconfirm, Space, Tag, Typography } from 'antd'
import { ArrowDownOutlined, ArrowUpOutlined, DeleteOutlined } from '@ant-design/icons'
import { n } from '../api/client'
import { describe, move, without, type Operation } from './operations'
import { useDeleteDraft, usePublishDraft, useSaveDraft, type Draft } from './queries'

/** The open draft: its operations in order, the server's dry-run verdict, and publish. */
export function DraftPanel({ draft }: { draft: Draft }) {
  const { message } = App.useApp()
  const save = useSaveDraft()
  const publish = usePublishDraft()
  const remove = useDeleteDraft()
  const [publishing, setPublishing] = useState(false)
  const [summary, setSummary] = useState('')

  const update = (operations: Operation[], title?: string) =>
    save.mutate({ draft, operations, title }, { onError: (e) => message.error(e.message) })

  const seedImport = draft.kind === 'SeedImport'
  return (
    <Card
      size="small"
      title={
        <Typography.Text editable={seedImport ? false : { onChange: (t) => t.trim() && t !== draft.title && update(draft.operations, t.trim()), maxLength: 120 }}>
          {draft.title}
        </Typography.Text>
      }
      extra={<Tag color={seedImport ? 'purple' : 'blue'}>{seedImport ? 'seed içe aktarma' : 'taslak'}</Tag>}
    >
      <Space direction="vertical" style={{ width: '100%' }}>
        {draft.isStale && (
          <Alert
            type="warning"
            showIcon
            message={`Taslak ${draft.basedOnNumber}. sürüme göre hazırlandı; güncel sürüm ${draft.currentNumber}.`}
            description={seedImport ? 'İçe aktarma taslağı her yayından sonra yeniden hesaplanır; sayfayı yenile.' : 'Kaydetmek taslağı güncel sürüme taşır; çakışan yeni konu kodları yenilenir.'}
            action={seedImport ? undefined : <Button size="small" onClick={() => update(draft.operations)}>Taşı</Button>}
          />
        )}
        {draft.validation.isValid ? (
          draft.operations.length > 0 && <Alert type="success" showIcon message="Taslak sorunsuz uygulanıyor." />
        ) : (
          <Alert type="error" showIcon message="Taslak bu haliyle yayınlanamaz" description={draft.validation.problem} />
        )}
        {draft.operations.length === 0 ? (
          <Empty description="Henüz değişiklik yok. Soldaki listeden konu, kaynak veya takvim düzenle." />
        ) : (
          <List
            size="small"
            dataSource={draft.operations}
            renderItem={(op, i) => (
              <List.Item
                actions={
                  seedImport
                    ? []
                    : [
                        <Button key="up" size="small" type="text" icon={<ArrowUpOutlined />} aria-label="Yukarı" disabled={i === 0} onClick={() => update(move(draft.operations, i, -1))} />,
                        <Button key="down" size="small" type="text" icon={<ArrowDownOutlined />} aria-label="Aşağı" disabled={i === draft.operations.length - 1} onClick={() => update(move(draft.operations, i, 1))} />,
                        <Button key="del" size="small" type="text" danger icon={<DeleteOutlined />} aria-label="Kaldır" onClick={() => update(without(draft.operations, i))} />,
                      ]
                }
              >
                <Typography.Text>
                  {i + 1}. {describe(op)}
                </Typography.Text>
              </List.Item>
            )}
          />
        )}
        <Space wrap>
          <Button
            type="primary"
            disabled={!draft.validation.isValid || draft.operations.length === 0 || draft.isStale}
            loading={publish.isPending}
            onClick={() => {
              setSummary(seedImport ? draft.title : '')
              setPublishing(true)
            }}
          >
            Yayınla ({n(draft.currentNumber) + 1}. sürüm)
          </Button>
          <Popconfirm title="Taslak silinsin mi?" okText="Sil" cancelText="Vazgeç" onConfirm={() => remove.mutate(draft.id, { onError: (e) => message.error(e.message) })}>
            <Button danger loading={remove.isPending}>
              Taslağı sil
            </Button>
          </Popconfirm>
          {save.isPending && <Typography.Text type="secondary">Kaydediliyor…</Typography.Text>}
        </Space>
      </Space>

      <Modal
        open={publishing}
        title="Yeni sürümü yayınla"
        okText="Yayınla"
        cancelText="Vazgeç"
        okButtonProps={{ disabled: !summary.trim(), loading: publish.isPending }}
        onCancel={() => setPublishing(false)}
        onOk={() =>
          publish.mutate(
            { draft, summary: summary.trim() },
            {
              onSuccess: (v) => {
                setPublishing(false)
                message.success(`${v.number}. sürüm yayınlandı. Uygulamalar bir sonraki açılışta günceller.`)
              },
              onError: (e) => message.error(e.message),
            },
          )
        }
      >
        <Typography.Paragraph>
          {draft.operations.length} değişiklik tek işlemde uygulanır. Öğrenci uygulamaları müfredatı ETag ile yeniler; analizler sıradaki hesapta yeni
          sürümü kullanır.
        </Typography.Paragraph>
        <Input.TextArea rows={3} maxLength={300} showCount placeholder="Kısa özet (geçmişte görünür)" value={summary} onChange={(e) => setSummary(e.target.value)} />
      </Modal>
    </Card>
  )
}
