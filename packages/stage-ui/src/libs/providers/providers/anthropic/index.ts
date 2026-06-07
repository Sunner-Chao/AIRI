import type { ModelInfo } from '../../types'

import { errorMessageFrom } from '@moeru/std'
import { createChatProvider, merge } from '@xsai-ext/providers/utils'
import { z } from 'zod'

import { defineProvider } from '../registry'

const ANTHROPIC_VERSION = '2023-06-01'
const LOCAL_ANTHROPIC_MESSAGES_PROXY_PATH = '/__airi/anthropic-messages-proxy'

const ANTHROPIC_MODELS: ModelInfo[] = [
  {
    id: 'claude-haiku-4-5-20251001',
    name: 'Claude Haiku 4.5',
    provider: 'anthropic',
    description: 'Anthropic fastest model with near-frontier intelligence',
  },
  {
    id: 'claude-sonnet-4-5-20250929',
    name: 'Claude Sonnet 4.5',
    provider: 'anthropic',
    description: 'Anthropic smartest model for complex agents and coding',
  },
  {
    id: 'claude-opus-4-1-20250805',
    name: 'Claude Opus 4.1',
    provider: 'anthropic',
    description: 'Exceptional model for specialized reasoning tasks',
  },
]

const MINIMAX_ANTHROPIC_MODELS: ModelInfo[] = [
  {
    id: 'MiniMax-M2.5',
    name: 'MiniMax M2.5',
    provider: 'anthropic',
    description: 'MiniMax model for Anthropic-compatible endpoints',
  },
  {
    id: 'MiniMax-M2.1',
    name: 'MiniMax M2.1',
    provider: 'anthropic',
    description: 'MiniMax model for Anthropic-compatible endpoints',
  },
  {
    id: 'MiniMax-M2',
    name: 'MiniMax M2',
    provider: 'anthropic',
    description: 'MiniMax model for Anthropic-compatible endpoints',
  },
]

const anthropicConfigSchema = z.object({
  apiKey: z
    .string('API Key'),
  baseUrl: z
    .string('Base URL')
    .optional()
    .default('https://api.anthropic.com/v1/'),
  modelId: z
    .string('Model ID')
    .optional()
    .default(''),
})

type AnthropicConfig = z.input<typeof anthropicConfigSchema>

function normalizeBaseUrl(baseUrl: unknown) {
  const resolved = typeof baseUrl === 'string' && baseUrl.trim()
    ? baseUrl.trim()
    : 'https://api.anthropic.com/v1/'

  return resolved.endsWith('/') ? resolved : `${resolved}/`
}

function anthropicMessagesUrl(baseUrl: unknown) {
  const raw = typeof baseUrl === 'string' && baseUrl.trim()
    ? baseUrl.trim()
    : 'https://api.anthropic.com/v1/'
  const parsed = new URL(raw)
  const normalizedPath = parsed.pathname.replace(/\/+$/, '')

  if (normalizedPath.endsWith('/messages')) {
    parsed.pathname = normalizedPath
    return parsed.toString()
  }

  if (normalizedPath.endsWith('/anthropic')) {
    parsed.pathname = `${normalizedPath}/v1/messages`
    return parsed.toString()
  }

  return new URL('messages', normalizeBaseUrl(raw)).toString()
}

function isMiniMaxAnthropicEndpoint(baseUrl: unknown) {
  if (typeof baseUrl !== 'string' || !baseUrl.trim())
    return false

  try {
    const parsed = new URL(baseUrl)
    return parsed.hostname.includes('minimax') || parsed.hostname.includes('minimaxi')
  }
  catch {
    return false
  }
}

function configuredModelId(config: Pick<AnthropicConfig, 'baseUrl' | 'modelId'>) {
  if (typeof config.modelId === 'string' && config.modelId.trim())
    return config.modelId.trim()

  if (isMiniMaxAnthropicEndpoint(config.baseUrl))
    return 'MiniMax-M2.5'

  return ''
}

function canUseLocalProxy() {
  if (typeof window === 'undefined')
    return false

  return ['localhost', '127.0.0.1', '::1'].includes(window.location.hostname)
}

function headersToObject(headers: HeadersInit | undefined): Record<string, string> {
  if (!headers)
    return {}

  if (headers instanceof Headers) {
    const result: Record<string, string> = {}
    headers.forEach((value, key) => {
      result[key] = value
    })
    return result
  }

  if (Array.isArray(headers))
    return Object.fromEntries(headers)

  return headers
}

function contentToAnthropicText(content: unknown): string {
  if (typeof content === 'string')
    return content

  if (Array.isArray(content)) {
    return content
      .map((part) => {
        if (typeof part === 'string')
          return part
        if (part && typeof part === 'object' && 'text' in part && typeof part.text === 'string')
          return part.text
        return ''
      })
      .filter(Boolean)
      .join('\n')
  }

  return content == null ? '' : String(content)
}

function toAnthropicPayload(openAICompatiblePayload: any, modelOverride?: string) {
  const system: string[] = []
  const messages = (openAICompatiblePayload.messages || [])
    .map((message: any) => {
      if (message.role === 'system') {
        const content = contentToAnthropicText(message.content)
        if (content)
          system.push(content)
        return null
      }

      if (message.role === 'tool') {
        return {
          role: 'user',
          content: contentToAnthropicText(message.content),
        }
      }

      if (message.role !== 'assistant' && message.role !== 'user')
        return null

      return {
        role: message.role,
        content: contentToAnthropicText(message.content),
      }
    })
    .filter(Boolean)

  return {
    max_tokens: openAICompatiblePayload.max_tokens ?? openAICompatiblePayload.maxTokens ?? 1024,
    messages,
    model: modelOverride || openAICompatiblePayload.model,
    ...(system.length > 0 ? { system: system.join('\n\n') } : {}),
    ...(openAICompatiblePayload.stream ? { stream: true } : {}),
  }
}

function fromAnthropicResponse(data: any, model: string) {
  const text = Array.isArray(data.content)
    ? data.content
        .filter((part: any) => part?.type === 'text' && typeof part.text === 'string')
        .map((part: any) => part.text)
        .join('')
    : ''

  return {
    choices: [
      {
        finish_reason: data.stop_reason ?? 'stop',
        index: 0,
        message: {
          content: text,
          role: 'assistant',
        },
      },
    ],
    created: Math.floor(Date.now() / 1000),
    id: data.id ?? `anthropic-${crypto.randomUUID()}`,
    model,
    object: 'chat.completion',
    system_fingerprint: '',
    usage: {
      completion_tokens: data.usage?.output_tokens ?? 0,
      prompt_tokens: data.usage?.input_tokens ?? 0,
      total_tokens: (data.usage?.input_tokens ?? 0) + (data.usage?.output_tokens ?? 0),
    },
  }
}

function encodeSse(data: unknown) {
  return `data: ${JSON.stringify(data)}\n\n`
}

function createAnthropicMessagesStream(response: Response, model: string) {
  const decoder = new TextDecoder()
  const encoder = new TextEncoder()
  let buffer = ''
  const id = `anthropic-${crypto.randomUUID()}`
  const created = Math.floor(Date.now() / 1000)

  return new ReadableStream({
    async start(controller) {
      const reader = response.body?.getReader()
      if (!reader) {
        controller.close()
        return
      }

      const enqueue = (event: any) => controller.enqueue(encoder.encode(encodeSse(event)))

      const handleEvent = (raw: string) => {
        const dataLine = raw.split('\n').find(line => line.startsWith('data:'))
        if (!dataLine)
          return

        const dataText = dataLine.slice(5).trim()
        if (!dataText || dataText === '[DONE]')
          return

        const data = JSON.parse(dataText)
        if (data.type === 'content_block_delta' && data.delta?.type === 'text_delta') {
          enqueue({
            choices: [{ delta: { content: data.delta.text }, index: 0 }],
            created,
            id,
            model,
            object: 'chat.completion.chunk',
          })
        }
        else if (data.type === 'message_delta') {
          enqueue({
            choices: [{ delta: {}, finish_reason: data.delta?.stop_reason ?? 'stop', index: 0 }],
            created,
            id,
            model,
            object: 'chat.completion.chunk',
            usage: {
              completion_tokens: data.usage?.output_tokens ?? 0,
              prompt_tokens: 0,
              total_tokens: data.usage?.output_tokens ?? 0,
            },
          })
        }
      }

      try {
        while (true) {
          const { done, value } = await reader.read()
          if (done)
            break

          buffer += decoder.decode(value, { stream: true })
          const events = buffer.split('\n\n')
          buffer = events.pop() ?? ''
          for (const event of events)
            handleEvent(event)
        }

        if (buffer)
          handleEvent(buffer)

        controller.enqueue(encoder.encode('data: [DONE]\n\n'))
        controller.close()
      }
      catch (error) {
        controller.error(error)
      }
    },
  })
}

async function fetchAnthropicMessagesWithLocalFallback(targetUrl: string, init: RequestInit) {
  try {
    return await fetch(targetUrl, init)
  }
  catch (error) {
    if (!canUseLocalProxy())
      throw error

    return fetch(LOCAL_ANTHROPIC_MESSAGES_PROXY_PATH, {
      ...init,
      headers: {
        ...headersToObject(init.headers),
        'x-airi-anthropic-messages-url': targetUrl,
      },
    })
  }
}

async function anthropicMessagesFetch(apiKey: string, baseUrl: string, modelOverride: string | undefined, _input: RequestInfo | URL, init: RequestInit = {}) {
  const rawBody = typeof init.body === 'string' ? JSON.parse(init.body) : {}
  const payload = toAnthropicPayload(rawBody, modelOverride)
  const targetUrl = anthropicMessagesUrl(baseUrl)
  const response = await fetchAnthropicMessagesWithLocalFallback(targetUrl, {
    body: JSON.stringify(payload),
    headers: {
      ...headersToObject(init.headers),
      'anthropic-dangerous-direct-browser-access': 'true',
      'anthropic-version': ANTHROPIC_VERSION,
      'content-type': 'application/json',
      'x-api-key': apiKey,
    },
    method: 'POST',
    signal: init.signal,
  })

  if (!response.ok)
    return response

  if (!payload.stream) {
    const data = await response.json()
    return Response.json(fromAnthropicResponse(data, payload.model))
  }

  return new Response(createAnthropicMessagesStream(response, payload.model), {
    headers: {
      'content-type': 'text/event-stream',
    },
    status: response.status,
    statusText: response.statusText,
  })
}

function createAnthropic(apiKey: string, baseURL: string = 'https://api.anthropic.com/v1/', modelOverride?: string) {
  return merge(
    createChatProvider({
      apiKey,
      baseURL: normalizeBaseUrl(baseURL),
      fetch: (input: RequestInfo | URL, init?: RequestInit) => anthropicMessagesFetch(apiKey, baseURL, modelOverride, input, init),
    }),
    {
      model: () => ({
        apiKey,
        baseURL: normalizeBaseUrl(baseURL),
      }),
    },
  )
}

async function validateAnthropicMessages(config: AnthropicConfig) {
  const model = configuredModelId(config) || ANTHROPIC_MODELS[0]!.id
  const targetUrl = anthropicMessagesUrl(config.baseUrl)
  const response = await fetchAnthropicMessagesWithLocalFallback(targetUrl, {
    body: JSON.stringify({
      max_tokens: 1,
      messages: [
        {
          content: 'ping',
          role: 'user',
        },
      ],
      model,
    }),
    headers: {
      'anthropic-dangerous-direct-browser-access': 'true',
      'anthropic-version': ANTHROPIC_VERSION,
      'content-type': 'application/json',
      'x-api-key': config.apiKey,
    },
    method: 'POST',
  })

  if (!response.ok) {
    const body = await response.text().catch(() => '')
    throw new Error(`HTTP ${response.status} from ${targetUrl}${body ? `: ${body}` : ''}`)
  }
}

export const providerAnthropic = defineProvider<AnthropicConfig>({
  id: 'anthropic',
  order: 6,
  name: 'Anthropic',
  nameLocalize: ({ t }) => t('settings.pages.providers.provider.anthropic.title'),
  description: 'anthropic.com',
  descriptionLocalize: ({ t }) => t('settings.pages.providers.provider.anthropic.description'),
  tasks: ['chat'],
  icon: 'i-lobe-icons:claude',
  iconColor: 'i-lobe-icons:claude-color',

  createProviderConfig: ({ t }) => anthropicConfigSchema.extend({
    apiKey: anthropicConfigSchema.shape.apiKey.meta({
      labelLocalized: t('settings.pages.providers.catalog.edit.config.common.fields.field.api-key.label'),
      descriptionLocalized: t('settings.pages.providers.catalog.edit.config.common.fields.field.api-key.description'),
      placeholderLocalized: t('settings.pages.providers.catalog.edit.config.common.fields.field.api-key.placeholder'),
      type: 'password',
    }),
    baseUrl: anthropicConfigSchema.shape.baseUrl.meta({
      labelLocalized: t('settings.pages.providers.catalog.edit.config.common.fields.field.base-url.label'),
      descriptionLocalized: t('settings.pages.providers.catalog.edit.config.common.fields.field.base-url.description'),
      placeholderLocalized: t('settings.pages.providers.catalog.edit.config.common.fields.field.base-url.placeholder'),
    }),
    modelId: anthropicConfigSchema.shape.modelId.meta({
      labelLocalized: 'Model ID',
      descriptionLocalized: 'Optional model ID override. Required for Anthropic-compatible endpoints that do not accept Claude model IDs.',
      placeholderLocalized: 'e.g. MiniMax-M2.5',
    }),
  }),
  createProvider(config) {
    return createAnthropic(config.apiKey, config.baseUrl, configuredModelId(config) || undefined)
  },

  extraMethods: {
    listModels: async (config) => {
      const customModelId = configuredModelId(config)
      const customModels = customModelId
        ? [{
          id: customModelId,
          name: customModelId,
          provider: 'anthropic',
          description: 'Configured Anthropic Messages model ID.',
        } satisfies ModelInfo]
        : []
      const minimaxModels = isMiniMaxAnthropicEndpoint(config.baseUrl) ? MINIMAX_ANTHROPIC_MODELS : []

      return [
        ...customModels,
        ...minimaxModels.filter(model => model.id !== customModelId),
        ...ANTHROPIC_MODELS.filter(model => model.id !== customModelId),
      ]
    },
  },
  validationRequiredWhen(config) {
    return !!config.apiKey?.trim()
  },
  validators: {
    validateConfig: [({ t }) => ({
      id: 'anthropic:check-config',
      name: t('settings.pages.providers.catalog.edit.validators.openai-compatible.check-config.title'),
      validator: async (config) => {
        const errors: Array<{ error: unknown }> = []
        const apiKey = typeof config.apiKey === 'string' ? config.apiKey.trim() : ''
        const baseUrl = typeof config.baseUrl === 'string' ? config.baseUrl.trim() : ''
        if (!apiKey)
          errors.push({ error: new Error('API key is required.') })
        if (!baseUrl)
          errors.push({ error: new Error('Base URL is required.') })

        if (baseUrl) {
          try {
            const parsed = new URL(baseUrl)
            if (!parsed.host)
              errors.push({ error: new Error('Base URL is not absolute. Check your input.') })
          }
          catch {
            errors.push({ error: new Error('Base URL is invalid. It must be an absolute URL.') })
          }
        }

        return {
          errors,
          reason: errors.length > 0 ? errors.map(item => (item.error as Error).message).join(', ') : '',
          reasonKey: '',
          valid: errors.length === 0,
        }
      },
    })],
    validateProvider: [({ t }) => ({
      id: 'anthropic:check-chat-completions',
      name: t('settings.pages.providers.catalog.edit.validators.openai-compatible.check-supports-chat-completion.title'),
      validator: async (config) => {
        const errors: Array<{ error: unknown }> = []
        try {
          await validateAnthropicMessages(config)
        }
        catch (error) {
          const message = errorMessageFrom(error) || 'Unknown error.'
          const corsHint = message.includes('Failed to fetch')
            ? ' This usually means the endpoint does not allow browser CORS requests; in local dev AIRI will retry through the local proxy when opened from localhost or 127.0.0.1.'
            : ''
          errors.push({ error: new Error(`Anthropic Messages check failed: ${message}.${corsHint}`) })
        }

        return {
          errors,
          reason: errors.length > 0 ? errors.map(item => (item.error as Error).message).join(', ') : '',
          reasonKey: '',
          valid: errors.length === 0,
        }
      },
    })],
  },
})
