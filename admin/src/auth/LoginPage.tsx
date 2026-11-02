import { useState } from 'react'
import { useLogin } from '@refinedev/core'
import { Alert, Button, Card, Flex, Form, Input, QRCode, Space, Typography } from 'antd'
import { api, call, type Schemas } from '../api/client'

type Step =
  | { kind: 'password' }
  | { kind: 'totp'; mfaToken: string }
  | { kind: 'setup'; mfaToken: string; secret?: string; uri?: string }
  | { kind: 'recovery'; tokens: Schemas['AuthTokens']; codes: string[] }

/** Password, then TOTP; the first sign-in enrols the authenticator and shows the recovery codes once. */
export function LoginPage() {
  const { mutate: login, isPending: finishing } = useLogin<{ tokens: Schemas['AuthTokens'] }>()
  const [step, setStep] = useState<Step>({ kind: 'password' })
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [useRecovery, setUseRecovery] = useState(false)

  const finish = (tokens: Schemas['AuthTokens']) =>
    login(
      { tokens },
      {
        onSuccess: (result) => !result.success && setError(result.error?.message ?? 'Giriş tamamlanamadı.'),
        onError: (e) => setError(e.message),
      },
    )

  async function run(action: () => Promise<void>) {
    setBusy(true)
    setError(null)
    try {
      await action()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Beklenmeyen hata.')
    } finally {
      setBusy(false)
    }
  }

  function next(result: Schemas['SignInResult']) {
    if (result.tokens) {
      finish(result.tokens)
    } else if (result.mfa?.step === 'Totp') {
      setStep({ kind: 'totp', mfaToken: result.mfa.mfaToken })
    } else if (result.mfa) {
      const mfaToken = result.mfa.mfaToken
      setStep({ kind: 'setup', mfaToken })
      void run(async () => {
        const setup = await call(api.POST('/v1/auth/mfa/totp/setup', { body: { mfaToken } }))
        setStep({ kind: 'setup', mfaToken, secret: setup.secret, uri: setup.otpAuthUri })
      })
    }
  }

  return (
    <Flex justify="center" align="center" style={{ minHeight: '100vh', padding: 16 }}>
      <Card title="Nirengi yönetim" style={{ width: 400, maxWidth: '100%' }}>
        {error && <Alert type="error" message={error} showIcon style={{ marginBottom: 16 }} />}

        {step.kind === 'password' && (
          <Form layout="vertical" onFinish={(v: Schemas['LoginCommand']) => run(async () => next(await call(api.POST('/v1/auth/login', { body: v }))))}>
            <Form.Item name="email" label="E-posta" rules={[{ required: true, type: 'email' }]}>
              <Input autoComplete="username" autoFocus />
            </Form.Item>
            <Form.Item name="password" label="Şifre" rules={[{ required: true }]}>
              <Input.Password autoComplete="current-password" />
            </Form.Item>
            <Button type="primary" htmlType="submit" block loading={busy}>
              Devam
            </Button>
          </Form>
        )}

        {step.kind === 'totp' && (
          <Form
            layout="vertical"
            onFinish={(v: { code: string }) =>
              run(async () =>
                next({
                  tokens: await call(
                    api.POST('/v1/auth/mfa/totp', {
                      body: { mfaToken: step.mfaToken, code: useRecovery ? null : v.code, recoveryCode: useRecovery ? v.code : null },
                    }),
                  ),
                  mfa: null,
                }),
              )
            }
          >
            <Form.Item name="code" label={useRecovery ? 'Kurtarma kodu' : 'Doğrulayıcı uygulamadaki 6 haneli kod'} rules={[{ required: true }]}>
              <Input autoComplete="one-time-code" inputMode={useRecovery ? 'text' : 'numeric'} autoFocus />
            </Form.Item>
            <Space direction="vertical" style={{ width: '100%' }}>
              <Button type="primary" htmlType="submit" block loading={busy || finishing}>
                Giriş yap
              </Button>
              <Button type="link" block onClick={() => setUseRecovery((r) => !r)}>
                {useRecovery ? 'Uygulama kodunu kullan' : 'Kurtarma kodu kullan'}
              </Button>
            </Space>
          </Form>
        )}

        {step.kind === 'setup' && (
          <Form
            layout="vertical"
            onFinish={(v: { code: string }) =>
              run(async () => {
                const enrolled = await call(api.POST('/v1/auth/mfa/totp/setup/confirm', { body: { mfaToken: step.mfaToken, code: v.code } }))
                setStep({ kind: 'recovery', tokens: enrolled.tokens, codes: enrolled.recoveryCodes })
              })
            }
          >
            <Typography.Paragraph>Yönetici hesabı iki adımlı doğrulama ister. Kodu doğrulayıcı uygulamayla tara.</Typography.Paragraph>
            {step.uri ? (
              <Flex vertical align="center" gap={8} style={{ marginBottom: 16 }}>
                <QRCode value={step.uri} />
                <Typography.Text copyable code>
                  {step.secret}
                </Typography.Text>
              </Flex>
            ) : (
              <Typography.Paragraph type="secondary">Hazırlanıyor…</Typography.Paragraph>
            )}
            <Form.Item name="code" label="Uygulamadaki 6 haneli kod" rules={[{ required: true, len: 6 }]}>
              <Input autoComplete="one-time-code" inputMode="numeric" />
            </Form.Item>
            <Button type="primary" htmlType="submit" block loading={busy} disabled={!step.uri}>
              Doğrula
            </Button>
          </Form>
        )}

        {step.kind === 'recovery' && (
          <Space direction="vertical" style={{ width: '100%' }}>
            <Alert type="warning" showIcon message="Kurtarma kodları yalnız şimdi gösterilir. Her biri bir kez kullanılır; güvenli bir yere kaydet." />
            <Typography.Paragraph copyable={{ text: step.codes.join('\n') }}>
              <pre style={{ margin: 0 }}>{step.codes.join('\n')}</pre>
            </Typography.Paragraph>
            <Button type="primary" block loading={finishing} onClick={() => finish(step.tokens)}>
              Kaydettim, devam et
            </Button>
          </Space>
        )}
      </Card>
    </Flex>
  )
}
