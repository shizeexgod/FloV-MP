import type { Config } from 'tailwindcss';

const config: Config = {
  content: [
    './src/pages/**/*.{js,ts,jsx,tsx,mdx}',
    './src/components/**/*.{js,ts,jsx,tsx,mdx}',
    './src/app/**/*.{js,ts,jsx,tsx,mdx}',
  ],
  theme: {
    extend: {
      colors: {
        background: '#08090c',
        foreground: '#e9eaee',
        ink: {
          950: '#08090c',
          900: '#0a0b0f',
          850: '#0c0e12',
          800: '#0d0f13',
          750: '#101318',
          700: '#14171d',
          600: '#1b1f27',
          500: '#252a34',
        },
        brand: {
          DEFAULT: '#ff3d8a',
          hover: '#ff5a9d',
          soft: '#ff8ab8',
          dark: '#c41d63',
        },
        // functional status colours only — used for badges/state, never decoration
        ok: '#3fb984',
        warn: '#d8a13a',
        err: '#e5484d',
        // legacy aliases kept so nothing breaks; visually muted toward neutral
        cyber: { DEFAULT: '#8b93a1', soft: '#aeb4bf', dark: '#6b7280' },
        cyan: { neon: '#8b93a1' },
        emeraldx: { DEFAULT: '#3fb984', soft: '#8fd9bd' },
        violetx: { DEFAULT: '#9aa0aa', soft: '#c2c6cd' },
      },
      backgroundImage: {
        'gradient-radial': 'radial-gradient(var(--tw-gradient-stops))',
      },
      fontFamily: {
        sans: ['Inter', 'ui-sans-serif', 'system-ui', '-apple-system', 'Segoe UI', 'Roboto', 'sans-serif'],
        mono: ['"JetBrains Mono"', 'ui-monospace', 'SFMono-Regular', 'Menlo', 'monospace'],
      },
      boxShadow: {
        'neon-pink': '0 10px 30px -14px rgba(255, 61, 138, 0.28)',
        'neon-cyan': '0 10px 30px -16px rgba(0, 0, 0, 0.5)',
        glass: '0 18px 50px -24px rgba(0, 0, 0, 0.6)',
      },
      keyframes: {
        'fade-in': {
          '0%': { opacity: '0', transform: 'translateY(6px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
        'fade-in-fast': { '0%': { opacity: '0' }, '100%': { opacity: '1' } },
        'scale-in': {
          '0%': { opacity: '0', transform: 'scale(0.98)' },
          '100%': { opacity: '1', transform: 'scale(1)' },
        },
        'slide-down': {
          '0%': { opacity: '0', transform: 'translateY(-8px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
        'pulse-ring': {
          '0%': { transform: 'scale(0.85)', opacity: '0.5' },
          '80%, 100%': { transform: 'scale(2)', opacity: '0' },
        },
      },
      animation: {
        'fade-in': 'fade-in 0.4s ease both',
        'fade-in-fast': 'fade-in-fast 0.25s ease both',
        'scale-in': 'scale-in 0.2s ease both',
        'slide-down': 'slide-down 0.24s ease both',
        'pulse-ring': 'pulse-ring 2.6s cubic-bezier(0.4, 0, 0.6, 1) infinite',
      },
    },
  },
  plugins: [],
};

export default config;
