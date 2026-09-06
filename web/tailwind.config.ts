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
        background: '#070a12',
        foreground: '#eef1f7',
        ink: {
          950: '#070a12',
          900: '#090d16',
          850: '#0b101c',
          800: '#0d121f',
          750: '#101728',
          700: '#131a2b',
          600: '#1a2338',
          500: '#232e48',
        },
        brand: {
          DEFAULT: '#ff3d8a',
          hover: '#ff5c9e',
          soft: '#ff8ab8',
          dark: '#c41d63',
          glow: 'rgba(255, 61, 138, 0.35)',
        },
        cyber: {
          DEFAULT: '#00f0ff',
          soft: '#7df6ff',
          dark: '#0891a3',
        },
        cyan: {
          neon: '#00f0ff',
        },
        emeraldx: {
          DEFAULT: '#10b981',
          soft: '#6ee7b7',
        },
        violetx: {
          DEFAULT: '#8b5cf6',
          soft: '#c4b5fd',
        },
      },
      backgroundImage: {
        'gradient-radial': 'radial-gradient(var(--tw-gradient-stops))',
        'grid-faint':
          'linear-gradient(to right, rgba(255,255,255,0.04) 1px, transparent 1px), linear-gradient(to bottom, rgba(255,255,255,0.04) 1px, transparent 1px)',
      },
      fontFamily: {
        sans: ['Inter', 'ui-sans-serif', 'system-ui', '-apple-system', 'Segoe UI', 'Roboto', 'sans-serif'],
        mono: ['"JetBrains Mono"', 'ui-monospace', 'SFMono-Regular', 'Menlo', 'monospace'],
      },
      boxShadow: {
        'neon-pink': '0 0 0 1px rgba(255,61,138,0.35), 0 8px 40px -8px rgba(255,61,138,0.45)',
        'neon-cyan': '0 0 0 1px rgba(0,240,255,0.30), 0 8px 40px -8px rgba(0,240,255,0.40)',
        glass: '0 24px 70px -24px rgba(0, 0, 0, 0.65)',
        'inner-glow': 'inset 0 1px 0 0 rgba(255,255,255,0.06)',
      },
      keyframes: {
        'fade-in': {
          '0%': { opacity: '0', transform: 'translateY(8px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
        'fade-in-fast': {
          '0%': { opacity: '0' },
          '100%': { opacity: '1' },
        },
        'scale-in': {
          '0%': { opacity: '0', transform: 'scale(0.96)' },
          '100%': { opacity: '1', transform: 'scale(1)' },
        },
        'slide-down': {
          '0%': { opacity: '0', transform: 'translateY(-10px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
        shimmer: {
          '100%': { transform: 'translateX(100%)' },
        },
        float: {
          '0%, 100%': { transform: 'translateY(0)' },
          '50%': { transform: 'translateY(-14px)' },
        },
        'pulse-ring': {
          '0%': { transform: 'scale(0.8)', opacity: '0.7' },
          '80%, 100%': { transform: 'scale(2.2)', opacity: '0' },
        },
        marquee: {
          '0%': { transform: 'translateX(0)' },
          '100%': { transform: 'translateX(-50%)' },
        },
      },
      animation: {
        'fade-in': 'fade-in 0.5s cubic-bezier(0.22, 1, 0.36, 1) both',
        'fade-in-fast': 'fade-in-fast 0.3s ease both',
        'scale-in': 'scale-in 0.25s cubic-bezier(0.22, 1, 0.36, 1) both',
        'slide-down': 'slide-down 0.28s cubic-bezier(0.22, 1, 0.36, 1) both',
        shimmer: 'shimmer 2s infinite',
        float: 'float 7s ease-in-out infinite',
        'pulse-ring': 'pulse-ring 2.4s cubic-bezier(0.4, 0, 0.6, 1) infinite',
        marquee: 'marquee 40s linear infinite',
      },
    },
  },
  plugins: [],
};

export default config;
