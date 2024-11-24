module.exports = {
  content: [
    "./index.html",
    "./src/**/*.{js,jsx,ts,tsx}", 
  ],
  theme: {
    extend: {
      screens: {
        '1920': '1920px', // Custom breakpoint for 1920px width
        '3xl': '3440px', // Ultra-wide screens
        'hd': '1280px', // Custom breakpoint for 1280px width
      },
      fontFamily: {
        inter: [ 'Inter', 'Roboto', 'Montserrat', 'sans-serif'], // Fallback order
      },
      colors: {
        body: '#333333', // Global default text color
        background: '#FFFFFF', // Global default backgroundColor
		limeGreen: '#98FF98', 
      },
    },
  },
  plugins: [],
};