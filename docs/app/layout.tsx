import { Footer, Layout, Navbar } from 'nextra-theme-docs'
import { Head } from 'nextra/components'
import { getPageMap } from 'nextra/page-map'
import type { ReactNode } from 'react'
import 'nextra-theme-docs/style.css'

export const metadata = {
  title: {
    default: 'CSharp Compound Docs',
    template: '%s | CSharp Compound Docs',
  },
  description: 'Documentation for the CSharp Compound Docs plugin — a GraphRAG-powered knowledge oracle for C#/.NET projects.',
}

export default async function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en" dir="ltr" suppressHydrationWarning>
      <Head />
      <body>
        <Layout
          navbar={
            <Navbar
              logo={<b>CSharp Compound Docs</b>}
              projectLink="https://github.com/michaelmccord/csharp-compound-engineering"
            />
          }
          footer={<Footer>MIT {new Date().getFullYear()} © CSharp Compound Docs</Footer>}
          docsRepositoryBase="https://github.com/michaelmccord/csharp-compound-engineering/tree/master/docs"
          pageMap={await getPageMap()}
        >
          {children}
        </Layout>
      </body>
    </html>
  )
}
