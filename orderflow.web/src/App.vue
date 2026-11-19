<script setup lang="ts">
  import { onMounted, onUnmounted, ref } from 'vue';
  import OrderForm from './components/OrderForm.vue';
  import OrderList from './components/OrderList.vue';
  import { fetchOrders, fetchProducts } from './services/ordersApi';
  import type { Order, Product } from './types/order';

  const products = ref<Product[]>([]);
  const orders = ref<Order[]>([]);
  const loadingOrders = ref(false);
  const listError = ref<string | null>(null);

  let pollingTimer: number | undefined;

  async function loadProducts() {
    try {
      products.value = await fetchProducts();
    } catch {
      // Si falla el catálogo inicial, el usuario aún puede escribir el SKU manualmente
    }
  }

  async function loadOrders(silent = false) {
    if (!silent) loadingOrders.value = true;
    try {
      orders.value = await fetchOrders();
      listError.value = null;
    } catch (err) {
      listError.value = err instanceof Error ? err.message : 'Error al consultar pedidos.';
    } finally {
      loadingOrders.value = false;
    }
  }

  onMounted(async () => {
    await Promise.all([loadProducts(), loadOrders()]);
    // Polling cada 2 segundos para reflejar transiciones Pending -> Confirmed / Rejected
    pollingTimer = window.setInterval(() => {
      void loadOrders(true);
    }, 2000);
  });

  onUnmounted(() => {
    if (pollingTimer !== undefined) {
      clearInterval(pollingTimer);
    }
  });
</script>

<template>
  <main class="container">
    <header class="header">
      <h1>OrderFlow — Panel de Pedidos</h1>
      <p>Registro operativo y seguimiento asíncrono de reservas de inventario</p>
    </header>

    <div class="layout">
      <OrderForm :products="products" @order-created="loadOrders(true)" />
      <OrderList :orders="orders"
                 :loading="loadingOrders"
                 :error="listError"
                 @refresh="loadOrders(false)" />
    </div>
  </main>
</template>

<style>
  *, *::before, *::after {
    box-sizing: border-box;
  }

  :root {
    font-family: system-ui, -apple-system, Segoe UI, Roboto, sans-serif;
    color: #0f172a;
    background-color: #f1f5f9;
  }

  body {
    margin: 0;
    background-color: #f1f5f9;
    color: #0f172a;
  }

  .container {
    max-width: 1220px;
    margin: 0 auto;
    padding: 2rem 1.25rem;
  }

  .header {
    margin-bottom: 1.5rem;
  }

    .header h1 {
      margin: 0 0 0.25rem;
      font-size: 1.75rem;
      color: #0f172a;
    }

    .header p {
      margin: 0;
      color: #475569;
    }

  .layout {
    display: grid;
    grid-template-columns: 350px 1fr;
    gap: 1.5rem;
    align-items: start;
  }

  @media (max-width: 900px) {
    .layout {
      grid-template-columns: 1fr;
    }
  }

  .card {
    background: #ffffff;
    color: #0f172a;
    border: 1px solid #cbd5e1;
    border-radius: 10px;
    padding: 1.5rem;
    box-shadow: 0 1px 3px rgba(0,0,0,0.06);
  }

    .card h2 {
      margin-top: 0;
      font-size: 1.15rem;
      color: #0f172a;
    }

  .form-grid {
    display: flex;
    flex-direction: column;
    gap: 1rem;
  }

  .field {
    display: flex;
    flex-direction: column;
    gap: 0.35rem;
  }

    .field label {
      font-weight: 600;
      font-size: 0.9rem;
      color: #1e293b;
    }

    .field input {
      padding: 0.55rem 0.75rem;
      border: 1px solid #cbd5e1;
      border-radius: 6px;
      font-size: 0.95rem;
      background: #ffffff;
      color: #0f172a;
    }

  .hint {
    color: #64748b;
    font-size: 0.78rem;
  }

  .btn-primary {
    background: #2563eb;
    color: #ffffff;
    border: none;
    border-radius: 6px;
    padding: 0.65rem 1rem;
    font-weight: 600;
    cursor: pointer;
  }

    .btn-primary:hover:not(:disabled) {
      background: #1d4ed8;
    }

    .btn-primary:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

  .btn-secondary {
    background: #f8fafc;
    color: #1e293b;
    border: 1px solid #cbd5e1;
    border-radius: 6px;
    padding: 0.45rem 0.85rem;
    font-size: 0.85rem;
    cursor: pointer;
    font-weight: 500;
  }

  .alert {
    margin-top: 1rem;
    padding: 0.75rem;
    border-radius: 6px;
    font-size: 0.9rem;
  }

  .alert-error {
    background: #fef2f2;
    color: #b91c1c;
    border: 1px solid #fecaca;
  }

  .alert-success {
    background: #f0fdf4;
    color: #15803d;
    border: 1px solid #bbf7d0;
  }

  .list-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 1rem;
  }

  .table-wrapper {
    overflow-x: auto;
  }

  table {
    width: 100%;
    border-collapse: collapse;
    font-size: 0.9rem;
    color: #0f172a;
  }

  th, td {
    text-align: left;
    padding: 0.7rem 0.6rem;
    border-bottom: 1px solid #e2e8f0;
    vertical-align: middle;
    white-space: nowrap;
  }

  th {
    color: #475569;
    font-weight: 600;
    background: #f8fafc;
  }

  .badge {
    display: inline-block;
    padding: 0.25rem 0.65rem;
    border-radius: 999px;
    font-size: 0.78rem;
    font-weight: 600;
  }

  .badge-pending {
    background: #fef9c3;
    color: #a16207;
  }

  .badge-confirmed {
    background: #dcfce7;
    color: #15803d;
  }

  .badge-rejected {
    background: #fee2e2;
    color: #b91c1c;
  }

  .rejection-reason {
    font-size: 0.75rem;
    color: #b91c1c;
    margin-top: 0.25rem;
    white-space: normal;
  }

  .empty-state {
    color: #64748b;
    text-align: center;
    padding: 2rem 0;
  }
</style>
