import type { Schemas } from '../api/client'

export type Operation = Schemas['CurriculumOperation']
export type OperationType = NonNullable<Operation['type']>

const sourceKinds: Record<string, string> = { Publisher: 'yayınevi', Official: 'resmi', Institution: 'kurum' }

/** One line per operation, in the panel's language; codes stay visible because they are permanent. */
export function describe(op: Operation): string {
  switch (op.type) {
    case 'topic.add':
      return `Yeni konu ${op.code ?? '(kod kaydedince atanır)'}: ${op.name} — ${op.subjectCode}`
    case 'topic.update':
      return `${op.code} güncellenir: ${op.name}`
    case 'topic.retire':
      return `${op.code} emekliye ayrılır`
    case 'topic.reactivate':
      return `${op.code} yeniden aktif`
    case 'topic.merge':
      return `${op.fromCodes.join(', ')} → ${op.toCode} birleşir (kayıtlar ${op.toCode}'ye sayılır)`
    case 'topic.split':
      return `${op.fromCode} → ${op.toCodes.join(', ')} bölünür (eski kayıtlar eski konuda kalır)`
    case 'source.add':
      return `Yeni kaynak ${op.code}: ${op.name} (${sourceKinds[op.kind] ?? op.kind})`
    case 'source.update':
      return `Kaynak ${op.code} güncellenir: ${op.name}`
    case 'source.retire':
      return `Kaynak ${op.code} emekliye ayrılır`
    case 'source.reactivate':
      return `Kaynak ${op.code} yeniden aktif`
    case 'calendar.set':
      return `${op.calendar.examYear} sınav takvimi değişir (${op.calendar.events.length} tarih)`
    default:
      return 'Bilinmeyen işlem'
  }
}

/** Topic codes an operation touches, to mark them in the tree. */
export function touchedTopics(op: Operation): string[] {
  switch (op.type) {
    case 'topic.add':
      return op.code ? [op.code] : []
    case 'topic.update':
    case 'topic.retire':
    case 'topic.reactivate':
      return [op.code]
    case 'topic.merge':
      return [...op.fromCodes, op.toCode]
    case 'topic.split':
      return [op.fromCode, ...op.toCodes]
    default:
      return []
  }
}

export function without<T>(list: readonly T[], index: number): T[] {
  return list.filter((_, i) => i !== index)
}

export function move<T>(list: readonly T[], index: number, delta: -1 | 1): T[] {
  const target = index + delta
  if (target < 0 || target >= list.length) return [...list]
  const copy = [...list]
  ;[copy[index], copy[target]] = [copy[target], copy[index]]
  return copy
}

/** Sum of a topic's expected questions over the given tests (the student's q_i, ADR-0019). */
export function expectedFor(weights: readonly Schemas['TopicWeightDto'][], tests: ReadonlySet<string>): number {
  return weights.filter((w) => tests.has(w.testCode)).reduce((sum, w) => sum + Number(w.expectedQuestions), 0)
}
