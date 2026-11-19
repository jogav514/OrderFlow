import type { CreateOrderPayload, Order, Product } from '../types/order';

const API_BASE_URL = (import.meta.env.VITE_API_URL as string | undefined) || 'http://localhost:5160';

/**
 * Extrae un mensaje de error legible desde respuestas 400/503 de ASP.NET Core.
 */
async function extractErrorMessage(response: Response): Promise<string> {
  try {
    const data = await response.json();
    if (typeof data?.error === 'string') return data.error;
    if (data?.errors && typeof data.errors === 'object') {
      const firstKey = Object.keys(data.errors)[0];
      if (firstKey && Array.isArray(data.errors[firstKey])) {
        return data.errors[firstKey][0];
      }
    }
    if (typeof data?.title === 'string') return data.title;
  } catch {
    // Si el cuerpo no es JSON, devolvemos mensaje genérico según código HTTP
  }
  return `Error del servidor (HTTP ${response.status}).`;
}

export async function fetchProducts(): Promise<Product[]> {
  const response = await fetch(`${API_BASE_URL}/products`);
  if (!response.ok) {
    throw new Error(await extractErrorMessage(response));
  }
  return response.json();
}

export async function fetchOrders(): Promise<Order[]> {
  const response = await fetch(`${API_BASE_URL}/orders`);
  if (!response.ok) {
    throw new Error(await extractErrorMessage(response));
  }
  return response.json();
}

export async function createOrder(payload: CreateOrderPayload): Promise<Order> {
  const response = await fetch(`${API_BASE_URL}/orders`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(payload),
  });

  if (!response.ok) {
    throw new Error(await extractErrorMessage(response));
  }
  return response.json();
}
