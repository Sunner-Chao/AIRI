<script setup lang="ts">
import { ProviderSettingsContainer, ProviderSettingsLayout } from '@proj-airi/stage-ui/components'
import { Callout } from '@proj-airi/ui'
import { useRouter } from 'vue-router'

const router = useRouter()
</script>

<template>
  <ProviderSettingsLayout
    provider-name="Official Provider"
    provider-icon-color="i-lobe-icons:airi-color"
    :on-back="() => router.push('/settings/providers')"
  >
    <ProviderSettingsContainer>
      <Callout theme="primary" label="Local mode">
        Official AIRI cloud providers and login are disabled in this local build.
      </Callout>
    </ProviderSettingsContainer>
  </ProviderSettingsLayout>
</template>
