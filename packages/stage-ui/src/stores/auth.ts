import type { Session, User } from 'better-auth'

import { StorageSerializers, useLocalStorage } from '@vueuse/core'
import { defineStore } from 'pinia'
import { computed, ref } from 'vue'

type AuthHook = () => void | Promise<void>
type TokenRefreshedHook = (accessToken: string) => void | Promise<void>

export const useAuthStore = defineStore('auth', () => {
  const user = useLocalStorage<User | null>('auth/v1/user', null, {
    serializer: StorageSerializers.object,
  })
  const session = useLocalStorage<Session | null>('auth/v1/session', null, {
    serializer: StorageSerializers.object,
  })
  const token = useLocalStorage<string | null>('auth/v1/token', null)
  const refreshToken = useLocalStorage<string | null>('auth/v1/refresh-token', null)
  const idToken = useLocalStorage<string | null>('auth/v1/oidc-id-token', null)
  const oidcClientId = useLocalStorage<string | null>('auth/v1/oidc-client-id', null)
  const tokenExpiry = useLocalStorage<number | null>('auth/v1/oidc-token-expiry', null)
  const credits = useLocalStorage<number>('user/v1/flux', 0)

  const needsLogin = ref(false)
  const isAuthenticated = computed(() => false)
  const userId = computed(() => 'local')

  function clearAllAuthState(): void {
    user.value = null
    session.value = null
    token.value = null
    refreshToken.value = null
    idToken.value = null
    oidcClientId.value = null
    tokenExpiry.value = null
    credits.value = 0
    needsLogin.value = false
  }

  function onAuthenticated(_hook: AuthHook) {
    return () => {}
  }

  function onLogout(_hook: AuthHook) {
    return () => {}
  }

  function onTokenRefreshed(_hook: TokenRefreshedHook) {
    return () => {}
  }

  async function updateCredits() {}
  async function restoreRefreshSchedule() {}
  async function refreshTokenNow() {
    return null
  }
  function scheduleTokenRefresh(_expiresInSeconds: number) {}

  clearAllAuthState()

  return {
    user,
    userId,
    session,
    token,
    refreshToken,
    idToken,
    isAuthenticated,
    credits,
    updateCredits,
    needsLogin,
    onAuthenticated,
    onLogout,
    oidcClientId,
    tokenExpiry,
    scheduleTokenRefresh,
    restoreRefreshSchedule,
    refreshTokenNow,
    clearAllAuthState,
    onTokenRefreshed,
  }
})
