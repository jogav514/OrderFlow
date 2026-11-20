<script setup lang="ts">
  import { computed, ref, watch } from 'vue';
  import type { Order } from '../types/order';

  const props = defineProps<{ orders: Order[]; loading: boolean; error: string | null }>();
  const emit = defineEmits<{ (e: 'refresh'): void }>();

  const currentPage = ref(1);
  const pageSize = 6;

  const totalPages = computed(() => Math.max(1, Math.ceil(props.orders.length / pageSize)));
  const paginatedOrders = computed(() => {
    const start = (currentPage.value - 1) * pageSize;
    return props.orders.slice(start, start + pageSize);
  });

  watch(() => props.orders.length, () => {
    if (currentPage.value > totalPages.value) currentPage.value = totalPages.value;
  });

  function formatDate(isoDate: string): string {
    return new Date(isoDate).toLocaleString('es-CO', { dateStyle: 'short', timeStyle: 'medium' });
  }
  function badgeClass(estado: Order['estado']): string {
    if (estado === 'Confirmed') return 'badge badge-confirmed';
    if (estado === 'Rejected') return 'badge badge-rejected';
    return 'badge badge-pending';
  }
</script>

<template>
  <section class="card">
    <div class="list-header">
      <div>
        <h2>Pedidos Registrados ({{ orders.length }})</h2>
        <small class="hint">Actualización automática cada 2 segundos</small>
      </div>
      <button type="button" class="btn-secondary" :disabled="loading" @click="emit('refresh')">
        {{ loading ? 'Actualizando...' : 'Refrescar ahora' }}
      </button>
    </div>

    <div v-if="error" class="alert alert-error" role="alert">{{ error }}</div>

    <div v-if="orders.length === 0 && !loading" class="empty-state">
      No hay pedidos registrados todavía. Crea el primero en el formulario.
    </div>

    <div v-else class="table-wrapper">
      <table>
        <thead>
          <tr><th>Id</th><th>Cliente</th><th>SKU</th><th>Cantidad</th><th>Fecha</th><th>Estado</th></tr>
        </thead>
        <tbody>
          <tr v-for="order in paginatedOrders" :key="order.id">
            <td :title="order.id"><code>{{ order.id.slice(0, 8) }}…</code></td>
            <td>{{ order.clienteNombre }}</td>
            <td><strong>{{ order.sku }}</strong></td>
            <td>{{ order.cantidad }}</td>
            <td>{{ formatDate(order.creadoEn) }}</td>
            <td>
              <span :class="badgeClass(order.estado)">{{ order.estado }}</span>
              <div v-if="order.motivoRechazo" class="rejection-reason">{{ order.motivoRechazo }}</div>
            </td>
          </tr>
        </tbody>
      </table>

      <div v-if="totalPages > 1" style="display:flex; justify-content:space-between; align-items:center; margin-top:1rem;">
        <button type="button" class="btn-secondary" :disabled="currentPage === 1" @click="currentPage--">Anterior</button>
        <span class="hint">Página {{ currentPage }} de {{ totalPages }}</span>
        <button type="button" class="btn-secondary" :disabled="currentPage === totalPages" @click="currentPage++">Siguiente</button>
      </div>
    </div>
  </section>
</template>
