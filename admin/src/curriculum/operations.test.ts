import { describe as suite, expect, it } from 'vitest'
import { describe, expectedFor, move, touchedTopics, without, type Operation } from './operations'

suite('operations', () => {
  it('describes merges with the lineage rule', () => {
    const op: Operation = { type: 'topic.merge', fromCodes: ['AYT.TAR.01', 'AYT.TAR.02'], toCode: 'AYT.TAR.03' }
    expect(describe(op)).toBe("AYT.TAR.01, AYT.TAR.02 → AYT.TAR.03 birleşir (kayıtlar AYT.TAR.03'ye sayılır)")
    expect(touchedTopics(op)).toEqual(['AYT.TAR.01', 'AYT.TAR.02', 'AYT.TAR.03'])
  })

  it('says when a new topic still waits for its code', () => {
    const op: Operation = {
      type: 'topic.add',
      code: null,
      subjectCode: 'TYT.TMAT',
      name: 'Yeni',
      subtopics: ['a'],
      gradeMin: 9,
      gradeMax: 9,
      description: null,
    }
    expect(describe(op)).toContain('kod kaydedince atanır')
    expect(touchedTopics(op)).toEqual([])
  })

  it('moves and removes without mutating', () => {
    const list = ['a', 'b', 'c']
    expect(move(list, 0, 1)).toEqual(['b', 'a', 'c'])
    expect(move(list, 0, -1)).toEqual(['a', 'b', 'c'])
    expect(without(list, 1)).toEqual(['a', 'c'])
    expect(list).toEqual(['a', 'b', 'c'])
  })

  it('sums weights over the score type tests', () => {
    const weights = [
      { testCode: 'AYT-TDE-SB1', expectedQuestions: 0.8, tier: 'High' as const },
      { testCode: 'AYT-SB2', expectedQuestions: 1.1, tier: 'High' as const },
    ]
    expect(expectedFor(weights, new Set(['AYT-TDE-SB1']))).toBeCloseTo(0.8)
    expect(expectedFor(weights, new Set(['AYT-TDE-SB1', 'AYT-SB2']))).toBeCloseTo(1.9)
  })
})
