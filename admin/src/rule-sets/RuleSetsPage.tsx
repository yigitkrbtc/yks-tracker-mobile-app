import { Alert, Card, Col, Descriptions, Row, Spin, Tag, Typography } from 'antd'
import { useRuleSets } from '../curriculum/queries'

/** Read only: rules change through code, the golden dataset and a version bump (ADR-0003, ADR-0017). */
export function RuleSetsPage() {
  const ruleSets = useRuleSets()
  if (ruleSets.isLoading) return <Spin />
  if (ruleSets.error) return <Alert type="error" showIcon message={ruleSets.error.message} />
  return (
    <>
      <Typography.Title level={4}>Kural setleri</Typography.Title>
      <Typography.Paragraph type="secondary">
        Parametreler koddadır; değişiklik golden dataset testinden geçip sürüm numarası artırılarak yapılır. Her sonuç hesaplandığı sürümü saklar.
      </Typography.Paragraph>
      <Row gutter={[16, 16]}>
        {ruleSets.data?.map((r) => (
          <Col key={r.module} xs={24} lg={12}>
            <Card title={r.name} extra={<Tag color="blue">{r.version}</Tag>} size="small">
              <Descriptions size="small" column={1} bordered items={r.parameters.map((p) => ({ key: p.name, label: p.name, children: p.value }))} />
            </Card>
          </Col>
        ))}
      </Row>
    </>
  )
}
