import type { OIDCFlowParams, TokenResponse } from './auth-oidc'

export type OAuthProvider = 'google' | 'github'
type LocalAuthError = { message?: string, status?: number } | null
type LocalAuthResponse<T> = Promise<{ data: T, error: LocalAuthError }>

function localResponse<T>(data: T): LocalAuthResponse<T> {
  return Promise.resolve({ data, error: null })
}

export function getAuthToken(): string | null {
  return null
}

export const authClient = {
  getSession: async () => localResponse(null),
  listSessions: async () => localResponse([]),
  listAccounts: async () => localResponse([]),
  unlinkAccount: async (_args: unknown) => localResponse(null),
  linkSocial: async (_args: unknown) => localResponse(null),
  updateUser: async (_args: unknown) => localResponse(null),
  changePassword: async (_args: unknown) => localResponse(null),
  requestPasswordReset: async (_args: unknown) => localResponse(null),
  deleteUser: async (_args: unknown) => localResponse(null),
  signIn: {
    social: async (_args: unknown) => localResponse(null),
  },
}

export async function initializeAuth() {}

export async function applyOIDCTokens(_tokens: TokenResponse, _clientId: string): Promise<void> {}

export async function fetchSession() {
  return false
}

export async function listSessions() {
  return { data: [] }
}

export async function signOut() {}

export async function signInOIDC(_params: OIDCFlowParams) {}

export async function triggerSignIn(_opts?: { provider?: OAuthProvider }): Promise<void> {}
