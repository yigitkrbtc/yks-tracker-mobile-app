import { useState } from 'react'
import { useNavigate } from 'react-router'
import { Alert, App, Button, Drawer, List, Popconfirm, Space, Spin, Table, Tag, Typography } from 'antd'
import dayjs from 'dayjs'
import { n, type Schemas } from '../api/client'
import { describe } from '../curriculum/operations'
import { useAdminCurriculum, useCreateDraft, useVersion, useVersions } from '../curriculum/queries'

type Version = Schemas['VersionSummaryDto']

/** Published versions, newest first. Going back is a new draft (a diff to the old snapshot), never a rewrite of history. */
export function VersionsPage() {
  const { message } = App.useApp()
  const navigate = useNavigate()
  const versions = useVersions()
  const curriculum = useAdminCurriculum()
  const [open, setOpen] = useState<number | null>(null)
  const detail = useVersion(open)
  const create = useCreateDraft()
  const current = curriculum.data ? n(curriculum.data.versionNumber) : null

  if (versions.error) return <Alert type="error" showIcon message={versions.error.message} />
  return (
    <Space direction="vertical" style={{ width: '100%' }}>
      <Typography.Title level={4}>Sürüm geçmişi</Typography.Title>
      <Table<Version>
        rowKey={(v) => n(v.number)}
        loading={versions.isLoading}
        dataSource={versions.data}
        pagination={{ pageSize: 25 }}
        scroll={{ x: 700 }}
        onRow={(v) => ({ onClick: () => setOpen(n(v.number)), style: { cursor: 'pointer' } })}
        columns={[
          {
            title: 'Sürüm',
            width: 110,
            render: (_, v) => (
              <Space>
                {n(v.number)}
                {n(v.number) === current && <Tag color="green">canlı</Tag>}
              </Space>
            ),
          },
          { title: 'Yayın', width: 150, render: (_, v) => dayjs(v.publishedAt).format('DD.MM.YYYY HH:mm') },
          { title: 'Özet', dataIndex: 'summary' },
          { title: 'Kaynak', width: 120, render: (_, v) => (v.isSeedImport ? <Tag color="purple">seed</Tag> : <Tag color="blue">panel</Tag>) },
          { title: 'İşlem', width: 80, render: (_, v) => n(v.operationCount) },
        ]}
      />
      <Drawer open={open !== null} width={640} title={open !== null ? `${open}. sürüm` : ''} onClose={() => setOpen(null)}>
        {detail.isLoading || !detail.data ? (
          <Spin />
        ) : (
          <Space direction="vertical" style={{ width: '100%' }}>
            <Typography.Paragraph>{detail.data.version.summary}</Typography.Paragraph>
            <Typography.Text type="secondary">
              {detail.data.snapshot.topics.filter((t) => t.isActive).length} aktif konu · {detail.data.snapshot.sources.filter((s) => s.isActive).length} aktif
              kaynak
            </Typography.Text>
            {open !== current && (
              <Popconfirm
                title={`${open}. sürüme dönüş taslağı oluşturulsun mu?`}
                description="Farklar taslak olur; yayınlayana kadar hiçbir şey değişmez. Ağırlıklar ve sınav yapısı seed'den gelir, geri alınmaz."
                okText="Oluştur"
                cancelText="Vazgeç"
                onConfirm={() =>
                  create.mutate(open, {
                    onSuccess: () => navigate('/'),
                    onError: (e) => message.error(e.message),
                  })
                }
              >
                <Button loading={create.isPending}>Bu sürüme dön</Button>
              </Popconfirm>
            )}
            <List
              size="small"
              header="Bu sürümdeki değişiklikler"
              dataSource={detail.data.operations}
              locale={{ emptyText: 'İlk sürüm (mevcut veriden)' }}
              renderItem={(op, i) => (
                <List.Item>
                  {i + 1}. {describe(op)}
                </List.Item>
              )}
            />
          </Space>
        )}
      </Drawer>
    </Space>
  )
}
