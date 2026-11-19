/** Representa un producto del catálogo retornado por GET /products */
export interface Product {
  sku: string;
  nombre: string;
}

/** Estados posibles de un pedido en OrderFlow */
export type OrderStatus = 'Pending' | 'Confirmed' | 'Rejected';

/** Representa un pedido retornado por Orders.Api */
export interface Order {
  id: string;
  clienteNombre: string;
  sku: string;
  cantidad: number;
  estado: OrderStatus;
  motivoRechazo: string | null;
  creadoEn: string;
}

/** Cuerpo enviado en POST /orders */
export interface CreateOrderPayload {
  clienteNombre: string;
  sku: string;
  cantidad: number;
}
