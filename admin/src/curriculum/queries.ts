import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, call, n, type Schemas } from '../api/client'
import type { Operation } from './operations'

export type Draft = Schemas['DraftResponse']
export type AdminCurriculum = Schemas['AdminCurriculumResponse']

export const keys = {
  curriculum: ['admin', 'curriculum'] as const,
  draft: (id: string) => ['admin', 'draft', id] as const,
  versions: ['admin', 'versions'] as const,
  version: (n: number) => ['admin', 'version', n] as const,
  ruleSets: ['admin', 'rule-sets'] as const,
}

export const useAdminCurriculum = () =>
  useQuery({ queryKey: keys.curriculum, queryFn: () => call(api.GET('/v1/admin/curriculum')) })

export const useDraft = (id: string | undefined) =>
  useQuery({
    queryKey: keys.draft(id ?? ''),
    queryFn: () => call(api.GET('/v1/admin/curriculum/drafts/{id}', { params: { path: { id: id! } } })),
    enabled: !!id,
  })

export const useVersions = () => useQuery({ queryKey: keys.versions, queryFn: () => call(api.GET('/v1/admin/curriculum/versions')) })

export const useVersion = (number: number | null) =>
  useQuery({
    queryKey: keys.version(number ?? 0),
    queryFn: () => call(api.GET('/v1/admin/curriculum/versions/{number}', { params: { path: { number: number! } } })),
    enabled: number !== null,
  })

export const useRuleSets = () => useQuery({ queryKey: keys.ruleSets, queryFn: () => call(api.GET('/v1/admin/rule-sets')) })

/** Opens the manual draft (or returns the open one); with a version number, a draft back to that version. */
export function useCreateDraft() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (fromVersion: number | null) => call(api.POST('/v1/admin/curriculum/drafts', { body: { fromVersion } })),
    onSuccess: (draft) => {
      client.setQueryData(keys.draft(draft.id), draft)
      void client.invalidateQueries({ queryKey: keys.curriculum })
    },
  })
}

/** Every edit saves the whole list: the server assigns codes and dry-runs the draft against the live curriculum. */
export function useSaveDraft() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ draft, title, operations }: { draft: Draft; title?: string; operations: Operation[] }) =>
      call(
        api.PUT('/v1/admin/curriculum/drafts/{id}', {
          params: { path: { id: draft.id } },
          body: { revision: n(draft.revision), title: title ?? draft.title, operations },
        }),
      ),
    onSuccess: (draft) => client.setQueryData(keys.draft(draft.id), draft),
  })
}

export function usePublishDraft() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ draft, summary }: { draft: Draft; summary: string }) =>
      call(
        api.POST('/v1/admin/curriculum/drafts/{id}/publish', {
          params: { path: { id: draft.id } },
          body: { revision: n(draft.revision), summary },
        }),
      ),
    onSuccess: (_, { draft }) => {
      client.removeQueries({ queryKey: keys.draft(draft.id) })
      void client.invalidateQueries({ queryKey: ['admin'] })
    },
  })
}

export function useDeleteDraft() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => call(api.DELETE('/v1/admin/curriculum/drafts/{id}', { params: { path: { id } } })),
    onSuccess: (_, id) => {
      client.removeQueries({ queryKey: keys.draft(id) })
      void client.invalidateQueries({ queryKey: keys.curriculum })
    },
  })
}

/** The live curriculum as data/seed files; fetched with the bearer token, then saved through a blob URL. */
export async function downloadSeedExport() {
  const { data, response } = await api.GET('/v1/admin/curriculum/export', { parseAs: 'blob' })
  if (!response.ok || !data) throw new Error(`Dışa aktarma başarısız (${response.status}).`)
  const name = /filename="?([^";]+)"?/.exec(response.headers.get('content-disposition') ?? '')?.[1] ?? 'nirengi-mufredat.zip'
  const url = URL.createObjectURL(data as Blob)
  const link = document.createElement('a')
  link.href = url
  link.download = name
  link.click()
  URL.revokeObjectURL(url)
}
