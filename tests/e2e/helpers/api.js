const { request } = require('@playwright/test')

const API_URL = process.env.PLAYWRIGHT_API_URL || 'http://localhost:5000'
const POS_API_URL = process.env.POS_API_URL || 'http://localhost:3000'
const TENANT_ID = process.env.PLAYWRIGHT_TENANT_ID || 'a0000000-0000-4000-8000-000000000001'

async function loginComoAdmin() {
  const posCtx = await request.newContext({
    baseURL: POS_API_URL,
    extraHTTPHeaders: { 'Content-Type': 'application/json' },
  })
  const res = await posCtx.post('/api/auth/login', {
    data: { tenant_id: TENANT_ID, email: 'admin@demo.pos', password: 'Admin123!' },
  })
  const body = await res.json()
  if (!body.data?.access_token) throw new Error(`POS login failed: ${JSON.stringify(body)}`)
  return body.data.access_token
}

async function getAuthContext() {
  const token = await loginComoAdmin()
  return await request.newContext({
    baseURL: API_URL,
    extraHTTPHeaders: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${token}`,
      'X-Tenant-Id': TENANT_ID,
    },
  })
}

module.exports = { getAuthContext, API_URL }
