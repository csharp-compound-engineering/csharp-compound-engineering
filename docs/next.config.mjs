import nextra from 'nextra'

const withNextra = nextra({})

const isProd = process.env.NODE_ENV === 'production'

export default withNextra({
  output: 'export',
  basePath: isProd ? '/csharp-compound-engineering' : '',
  assetPrefix: isProd ? '/csharp-compound-engineering/' : undefined,
  trailingSlash: true,
  images: {
    unoptimized: true,
  },
})
