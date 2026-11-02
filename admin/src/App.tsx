import { Authenticated, Refine } from '@refinedev/core'
import { ErrorComponent, ThemedLayout, useNotificationProvider } from '@refinedev/antd'
import routerProvider, { CatchAllNavigate, DocumentTitleHandler, NavigateToResource } from '@refinedev/react-router'
import { BookOutlined, HistoryOutlined, SettingOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import 'dayjs/locale/tr'
import { App as AntdApp, ConfigProvider, theme } from 'antd'
import trTR from 'antd/locale/tr_TR'
import { QueryClient } from '@tanstack/react-query'
import { BrowserRouter, Outlet, Route, Routes } from 'react-router'
import { authProvider } from './auth/authProvider'
import { LoginPage } from './auth/LoginPage'
import { CurriculumPage } from './curriculum/CurriculumPage'
import { RuleSetsPage } from './rule-sets/RuleSetsPage'
import { VersionsPage } from './versions/VersionsPage'
import { ApiError } from './api/client'
import { appName } from './config'

dayjs.locale('tr')

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: (count, error) => !(error instanceof ApiError && error.status < 500) && count < 2, refetchOnWindowFocus: false },
  },
})

/** Per-request nonce the API writes into index.html; absent in `npm run dev`. */
const nonce = document.querySelector<HTMLMetaElement>('meta[name="csp-nonce"]')?.content
const csp = nonce && !nonce.startsWith('__') ? { nonce } : undefined

/** Refine's own labels (menu, error page); the panel's text is written in Turkish directly. */
const messages: Record<string, string> = {
  'buttons.logout': 'Çıkış',
  'pages.error.404': 'Aradığın sayfa yok.',
  'pages.error.backHome': 'Müfredata dön',
  'pages.error.info': 'Bu işlem için yetkin yok.',
  'warnWhenUnsavedChanges': 'Kaydedilmemiş değişiklikler var. Çıkılsın mı?',
}

const i18nProvider = {
  translate: (key: string, _options?: unknown, defaultMessage?: string) => messages[key] ?? defaultMessage ?? key,
  changeLocale: async () => undefined,
  getLocale: () => 'tr',
}

const dark = typeof window !== 'undefined' && window.matchMedia?.('(prefers-color-scheme: dark)').matches

export default function App() {
  return (
    <BrowserRouter basename="/admin">
      <ConfigProvider csp={csp} locale={trTR} theme={{ algorithm: dark ? theme.darkAlgorithm : theme.defaultAlgorithm }}>
        <AntdApp>
          <Refine
            routerProvider={routerProvider}
            authProvider={authProvider}
            i18nProvider={i18nProvider}
            notificationProvider={useNotificationProvider}
            options={{ reactQuery: { clientConfig: queryClient }, disableTelemetry: true, title: { text: `${appName} yönetim` } }}
            resources={[
              { name: 'curriculum', list: '/', meta: { label: 'Müfredat', icon: <BookOutlined /> } },
              { name: 'versions', list: '/versions', meta: { label: 'Sürüm geçmişi', icon: <HistoryOutlined /> } },
              { name: 'rule-sets', list: '/rule-sets', meta: { label: 'Kural setleri', icon: <SettingOutlined /> } },
            ]}
          >
            <Routes>
              <Route
                element={
                  <Authenticated key="app" fallback={<CatchAllNavigate to="/login" />}>
                    <ThemedLayout>
                      <Outlet />
                    </ThemedLayout>
                  </Authenticated>
                }
              >
                <Route index element={<CurriculumPage />} />
                <Route path="/versions" element={<VersionsPage />} />
                <Route path="/rule-sets" element={<RuleSetsPage />} />
                <Route path="*" element={<ErrorComponent />} />
              </Route>
              <Route
                element={
                  <Authenticated key="login" fallback={<Outlet />}>
                    <NavigateToResource resource="curriculum" />
                  </Authenticated>
                }
              >
                <Route path="/login" element={<LoginPage />} />
              </Route>
            </Routes>
            <DocumentTitleHandler handler={({ resource }) => `${resource?.meta?.label ?? 'Giriş'} · ${appName} yönetim`} />
          </Refine>
        </AntdApp>
      </ConfigProvider>
    </BrowserRouter>
  )
}
