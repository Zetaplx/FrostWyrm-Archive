import {defineConfig} from '@playwright/test';
export default defineConfig({
  testDir: '.', testMatch: '*.spec.mjs', timeout: 90000, expect: {timeout: 15000}, workers: 1,
  use: {baseURL: (process.env.CRYPTID_BASE_URL || 'http://127.0.0.1:5010').replace(/\/?$/, '/'), headless: true, screenshot: 'only-on-failure',
    launchOptions: process.env.CHROMIUM_EXECUTABLE_PATH ? {executablePath: process.env.CHROMIUM_EXECUTABLE_PATH} : {}},
  webServer: process.env.CRYPTID_BASE_URL ? undefined : {command: 'dotnet run --project ../../apps/CryptidCompanion --no-build --no-launch-profile --urls http://127.0.0.1:5010', url:'http://127.0.0.1:5010', reuseExistingServer: !process.env.CI}
});
