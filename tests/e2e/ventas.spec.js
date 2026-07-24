const { test, expect } = require('@playwright/test')
const { getAuthContext } = require('./helpers/api')

test.describe('Ventas API', () => {
  test('GET /api/reportes/pos/ventas — debe devolver datos de ventas', async () => {
    const ctx = await getAuthContext()
    const res = await ctx.get('/api/reportes/pos/ventas')
    expect(res.status()).toBe(200)
  })

  test('GET /api/reportes/pos/ventas?formato=pdf — debe devolver PDF', async () => {
    const ctx = await getAuthContext()
    const res = await ctx.get('/api/reportes/pos/ventas', {
      params: { formato: 'pdf' },
    })
    expect(res.status()).toBe(200)
    const contentType = res.headers()['content-type'] || ''
    expect(contentType).toContain('application/pdf')
  })
})
