<script setup lang="ts">
import { Callout } from '@proj-airi/ui'
</script>

<template>
  <Callout theme="primary" label="Local mode">
    Account login, cloud identity, and Flux billing are disabled in this local build.
  </Callout>
</template>

<route lang="yaml">
meta:
  layout: settings
  titleKey: settings.pages.account.title
  subtitleKey: settings.title
  descriptionKey: settings.pages.account.description
  icon: i-solar:user-circle-bold-duotone
  settingsEntry: false
  order: 0
  stageTransition:
    name: slide
</route>
