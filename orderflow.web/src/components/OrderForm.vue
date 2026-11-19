<script setup lang="ts">
import { ref } from 'vue';
import { createOrder } from '../services/ordersApi';
import type { Order, Product } from '../types/order';

defineProps<{
  products: Product[];
}>();

const emit = defineEmits<{
  (e: 'order-created', order: Order): void;
}>();

const clienteNombre = ref('');
const sku = ref('SKU-001');
const cantidad = ref<number>(1);

const loading = ref(false);
const errorMessage = ref<string | null>(null);
const successMessage = ref<string | null>(null);

async function handleSubmit() {
  errorMessage.value = null;
  successMessage.value = null;

  // Validaciones visibles en pantalla antes de enviar
  if (!clienteNombre.value.trim()) {
    errorMessage.value = 'El nombre del cliente es obligatorio.';
    return;
  }

  if (!sku.value.trim()) {
    errorMessage.value = 'Debes indicar un SKU válido.';
    return;
  }

  if (!Number.isInteger(cantidad.value) || cantidad.value < 1 || cantidad.value > 100) {
    errorMessage.value = 'La cantidad debe ser un número entero entre 1 y 100.';
    return;
  }

  loading.value = true;
  try {
    const created = await createOrder({
      clienteNombre: clienteNombre.value.trim(),
      sku: sku.value.trim(),
      cantidad: cantidad.value,
    });

    successMessage.value = `Pedido registrado (${created.id.slice(0, 8)}...) en estado ${created.estado}.`;
    clienteNombre.value = '';
    cantidad.value = 1;
    emit('order-created', created);
  } catch (err) {
    errorMessage.value = err instanceof Error ? err.message : 'No se pudo registrar el pedido.';
  } finally {
    loading.value = false;
  }
}
</script>

<template>
  <section class="card">
    <h2>Registrar Pedido</h2>

    <form @submit.prevent="handleSubmit" class="form-grid" novalidate>
      <div class="field">
        <label for="cliente">Nombre del cliente</label>
        <input id="cliente"
               v-model="clienteNombre"
               type="text"
               placeholder="Ej. María Gómez"
               :disabled="loading" />
      </div>

      <div class="field">
        <label for="sku">Producto (SKU)</label>
        <input id="sku"
               v-model="sku"
               list="catalog-skus"
               placeholder="Ej. SKU-001"
               :disabled="loading" />
        <datalist id="catalog-skus">
          <option v-for="p in products" :key="p.sku" :value="p.sku">
            {{ p.sku }} — {{ p.nombre }}
          </option>
        </datalist>
        <small class="hint">
          Catálogo inicial: SKU-001 (10), SKU-002 (5), SKU-003 (20), SKU-004 (0 stock).
        </small>
      </div>

      <div class="field">
        <label for="cantidad">Cantidad (1–100)</label>
        <input id="cantidad"
               v-model.number="cantidad"
               type="number"
               min="1"
               max="100"
               :disabled="loading" />
      </div>

      <button type="submit" class="btn-primary" :disabled="loading">
        {{ loading ? 'Enviando pedido...' : 'Crear pedido' }}
      </button>
    </form>

    <div v-if="errorMessage" class="alert alert-error" role="alert">
      {{ errorMessage }}
    </div>

    <div v-if="successMessage" class="alert alert-success" role="status">
      {{ successMessage }}
    </div>
  </section>
</template>
