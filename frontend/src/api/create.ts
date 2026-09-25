import { createHttpApi } from './http';
export const mockMode = import.meta.env.DEV && import.meta.env.MODE === 'mock';
export async function createApi() {
    if (import.meta.env.DEV && import.meta.env.MODE === 'mock') {
        const { createMockApi } = await import('./mock');
        return createMockApi();
    }
    return createHttpApi();
}
