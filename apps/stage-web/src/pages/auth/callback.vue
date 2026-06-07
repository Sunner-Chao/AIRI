<script setup lang="ts">
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'

const router = useRouter()

onMounted(() => {
  router.replace('/')
})
</script>

<template>
  <main min-h-screen flex items-center justify-center>
    <span text="neutral-500 dark:neutral-400">Local mode: authentication is disabled.</span>
  </main>
</template>
