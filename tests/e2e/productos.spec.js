const { test, expect } = require('@playwright/test')
const { getAuthContext } = require('./helpers/api')

test.describe('Reportes de Productos', () => {
  test('GET /api/reportes/pos/productos — debe devolver datos de productos', async () => {
    const ctx = await getAuthContext()
    const res = await ctx.get('/api/reportes/pos/productos')
    expect(res.status()).toBe(200)
  })
})
