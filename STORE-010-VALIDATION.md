# NEXUS 0.10 — pacote submetido e limites de validação

Candidato de produção 0.10.0.0 x64, código 41d5f5b6c8e5ff86d7abc3b14298d53c3f29b0f4.
SHA256: AB9EEB3105BFE2397BC2EFE7ED305992DD881C52C941132C501115C6D2D3716C.
Tamanho: 91 944 036 bytes. Produto Store: 9NZ2PH44V2TV.

## Testes concluídos em 16/10/2026

- Build sem avisos/erros; 272 testes passaram, cinco ignorados. Inclui 47 casos específicos do WARP.
- Quatro testes nativos passaram em Windows descartável. Uma sessão preparou cinco valores diferentes, alterou-os e restaurou-os realmente.
- Quinze renderizações WinUI portáteis, incluindo VPN em tema claro/escuro e Defender, sem operações de ligação/análise.
- O pacote exato foi verificado por hash antes da validação instalada. Com a identidade de teste isolada, passaram nove arranques, verificação da identidade, alteração de preferência com Undo, atualização de metadados 0.10.0.0 para 0.10.1.0, retenção da base de dados, desinstalação/reinstalação. Todas as preferências e o ambiente descartável foram restaurados. Não valida migração binária da 0.7.

[Compilação e testes](https://github.com/joao01dinis-ctrl/NEXUS-Optimizer/actions/runs/38961709553) · [Validação instalada do mesmo conteúdo](https://github.com/joao01dinis-ctrl/NEXUS-Optimizer/actions/runs/38964189082) · [Revisão do código](https://github.com/joao01dinis-ctrl/NEXUS-Optimizer/pull/5).

## Distinções necessárias

O candidato de produção não foi instalado como pacote sem assinatura no PC do utilizador. Os ensaios instalados usam uma identidade de teste apenas no Windows descartável; não são assinatura ou certificação da Microsoft.

O novo executável com WARP foi recusado pelo App Control no portátil. Nenhuma política do Windows foi alterada. A publicação de base 0.10 com Defender, sem o novo WARP, abriu normalmente neste PC com os dados existentes e Smart App Control=1. É a alternativa local de Start-Nexus.cmd até existir uma instalação oficial Store 0.10 ou superior.

WARP não foi instalado, configurado ou ligado neste PC. Os testes de controlo usam substitutos e não são apresentados como um túnel real. Uma futura ligação exige o cliente oficial Cloudflare, os termos aceites pelo utilizador e confirmação separada. Defender não fez análises nem atualizações sobre dados pessoais durante estes ensaios. Não se alegam ganhos FiveM, ausência de malware, anonimato ou ausência de fugas DNS/IPv6.

## Conteúdo da página Store

Descrição e novidades atualizadas para a 0.10, política de privacidade completa com Defender/WARP/OpenAI opcional e declaração de IA generativa. Seis capturas nativas do Windows de testes, sem WARP instalado, com legendas explícitas; os ficheiros enviados foram comparados por hash com as capturas locais. Mantém preço gratuito e publicação automática após aprovação. Os pedidos runFullTrust/unvirtualizedResources foram documentados, sem ampliar o manifesto ou pedir desativação de segurança.

Submetida em 16/10/2026 depois do cancelamento e substituição autorizados da 0.7. O portal confirmou Em certificação, submissão concluída, pré-processamento em curso, certificação e publicação ainda não iniciadas. Publicação automática após aprovação. A submissão 1152921505702079774 foi reutilizada com o novo pacote 0.10.0.0; não se trata de uma nova identidade de produto. Não está certificada, assinada ou instalada pela Store neste PC. O acompanhamento diário foi atualizado para esta versão.