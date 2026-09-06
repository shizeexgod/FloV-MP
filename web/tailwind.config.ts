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
        background: '#090b10',
        foreground: '#f3f4f6',
        surface: {
          50: '#1e2436',
          100: '#171c2b',
          200: '#121622',
          300: '#0d101a',
          400: '#090b10',
        },
        brand: {
          DEFAULT: '#ff3d8a',
          hover: '#ff5599',
          glow: 'rgba(255, 61, 138, 0.35)',
          dark: '#c41d63',
        },
        cyan: {
          neon: '#00f0ff',
        },
      },
      backgroundImage: {
        'gradient-radial': 'radial-gradient(var(--tw-gradient-stops))',
        'hero-pattern': 'radial-gradient(circle at 50% 20%, rgba(255,61,138,0.12) 0%, rgba(9,11,16,0) 70%)',
      },
      fontFamily: {
        sans: ['Inter', 'system-ui', '-apple-system', 'sans-serif'],
        mono: ['JetBrains Mono', 'Menlo', 'monospace'],
      },
      boxShadow: {
        'neon-pink': '0 0 25px -3px rgba(255, 61, 138, 0.4)',
        'neon-cyan': '0 0 25px -3px rgba(0, 240, 255, 0.35)',
        'glass': '0 8px 32px 0 rgba(0, 0, 0, 0.37)',
      },
    },
  },
  plugins: [],
};

export default config;
