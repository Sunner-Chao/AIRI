import { afterEach, describe, expect, it, vi } from 'vitest'

import { providerAnthropic } from './index'

const t = (key: string) => key

function createValidator() {
  const [create] = providerAnthropic.validators!.validateProvider!
  return create({ t }).validator
}

describe('anthropic provider', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('appends messages to a version base URL', async () => {
    const fetchMock = vi.fn(async () => Response.json({
      content: [{ text: 'ok', type: 'text' }],
      id: 'msg_1',
      usage: { input_tokens: 1, output_tokens: 1 },
    }))
    vi.stubGlobal('fetch', fetchMock)

    const validator = createValidator()
    const result = await validator({
      apiKey: 'test-key',
      baseUrl: 'http://example.test/v1/',
    }, {} as any, {} as any, { t })

    expect(result.valid).toBe(true)
    expect(fetchMock).toHaveBeenCalledWith('http://example.test/v1/messages', expect.any(Object))
  })

  it('uses a full /messages base URL as-is without appending or trailing slash', async () => {
    const fetchMock = vi.fn(async () => Response.json({
      content: [{ text: 'ok', type: 'text' }],
      id: 'msg_1',
      usage: { input_tokens: 1, output_tokens: 1 },
    }))
    vi.stubGlobal('fetch', fetchMock)

    const validator = createValidator()
    const result = await validator({
      apiKey: 'test-key',
      baseUrl: 'http://example.test/v1/messages',
    }, {} as any, {} as any, { t })

    expect(result.valid).toBe(true)
    expect(fetchMock).toHaveBeenCalledWith('http://example.test/v1/messages', expect.any(Object))
  })

  it('expands a provider anthropic base URL to anthropic/v1/messages', async () => {
    const fetchMock = vi.fn(async () => Response.json({
      content: [{ text: 'ok', type: 'text' }],
      id: 'msg_1',
      usage: { input_tokens: 1, output_tokens: 1 },
    }))
    vi.stubGlobal('fetch', fetchMock)

    const validator = createValidator()
    const result = await validator({
      apiKey: 'test-key',
      baseUrl: 'http://example.test/anthropic',
    }, {} as any, {} as any, { t })

    expect(result.valid).toBe(true)
    expect(fetchMock).toHaveBeenCalledWith('http://example.test/anthropic/v1/messages', expect.any(Object))
  })

  it('defaults MiniMax Anthropic endpoints to a MiniMax model ID', async () => {
    const fetchMock = vi.fn(async () => Response.json({
      content: [{ text: 'ok', type: 'text' }],
      id: 'msg_1',
      usage: { input_tokens: 1, output_tokens: 1 },
    }))
    vi.stubGlobal('fetch', fetchMock)

    const validator = createValidator()
    const result = await validator({
      apiKey: 'test-key',
      baseUrl: 'https://api.minimaxi.com/anthropic',
    }, {} as any, {} as any, { t })

    const [, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit]
    expect(result.valid).toBe(true)
    expect(JSON.parse(init.body as string).model).toBe('MiniMax-M2.5')
  })
})
